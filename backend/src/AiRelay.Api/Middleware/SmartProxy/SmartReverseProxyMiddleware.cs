using AiRelay.Api.Authentication;
using AiRelay.Api.HostedServices.Workers;
using AiRelay.Api.Middleware.SmartProxy.ErrorHandling;
using AiRelay.Api.Middleware.SmartProxy.RequestProcessor;
using AiRelay.Application.ProviderAccounts.AppServices;
using AiRelay.Application.ProviderGroups.AppServices;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.ProviderGroups.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Leistd.Exception.Core;
using Leistd.Tracing.Core.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AiRelay.Api.Middleware.SmartProxy;

public class SmartReverseProxyMiddleware(
    ISmartProxyAppService smartProxyAppService,
    IDownstreamRequestProcessor downstreamRequestProcessor,
    IChatModelHandlerFactory chatModelHandlerFactory,
    ProxyErrorFormatterFactory errorFormatterFactory,
    SseResponseStreamProcessor streamProcessor,
    AccountUsageRecordHostedService usageRecordHostedService,
    IOptions<UsageLoggingOptions> loggingOptions,
    IConcurrencyStrategy concurrencyStrategy,
    ICorrelationIdProvider correlationIdProvider,
    AccountFingerprintAppService fingerprintAppService,
    ILogger<SmartReverseProxyMiddleware> logger)
{
    private readonly UsageLoggingOptions _loggingOptions = loggingOptions.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var (platform, apiKeyId, apiKeyName) = ValidateAndGetContext(context);
        var correlationId = correlationIdProvider.Get() ?? correlationIdProvider.Create();

        var chatModelHandler = chatModelHandlerFactory.CreateHandler(platform);
        var downContext = await downstreamRequestProcessor.ProcessAsync(context, chatModelHandler, apiKeyId, context.RequestAborted);

        try
        {
            var excludedAccountIds = new HashSet<Guid>();
            var accountSwitchCount = 0;
            const int MaxAccountSwitches = 10;

            while (true)
            {
                // 账号切换次数检查
                if (accountSwitchCount >= MaxAccountSwitches)
                {
                    throw new BadRequestException($"已尝试 {MaxAccountSwitches} 个账号，均不可用");
                }

                // 1. 选号
                var selectResult = await smartProxyAppService.SelectAccountAsync(
                        new SelectProxyAccountInputDto
                        {
                            Platform = platform,
                            ApiKeyId = apiKeyId,
                            ApiKeyName = apiKeyName,
                            SessionHash = downContext.SessionId,
                            ExcludedAccountIds = excludedAccountIds,
                            ModelId = downContext.ModelId
                        },
                        context.RequestAborted);

                var requiresFingerprint = selectResult.AllowOfficialClientMimic;

                if (requiresFingerprint)
                {
                    // 生成稳定的 Sticky Session ID 和 指纹 ClientID
                    downContext.StickySessionId = await fingerprintAppService.GenerateSessionUuidAsync(
                        selectResult.AccountToken.Id,
                        downContext.SessionId,
                        selectResult.AccountToken.ExtraProperties.TryGetValue("session_id_masking_enabled", out var maskingValue) && bool.TryParse(maskingValue, out var enabled) && enabled,
                        context.RequestAborted);

                    var fingerprint = await fingerprintAppService.GetOrCreateFingerprintAsync(
                        selectResult.AccountToken.Id,
                        downContext.Headers,
                        context.RequestAborted);
                    downContext.FingerprintClientId = fingerprint.ClientId;
                }

                var currentAccountRetryCount = 0;
                var shouldSwitchAccount = false;
                var degradationLevel = 0; // 降级级别计数器（0=正常, 1=移除签名, 2=移除函数）

                // 内层循环：同账号重试
                while (!shouldSwitchAccount)
                {
                    var activeRequestId = Guid.CreateVersion7();

                    // 2. 获取并发槽位
                    var acquired = await concurrencyStrategy.AcquireSlotAsync(selectResult.AccountToken.Id, activeRequestId, selectResult.WaitPlan.MaxConcurrency, context.RequestAborted);
                    if (!acquired)
                    {
                        if (selectResult.WaitPlan.ShouldWait)
                        {
                            // 场景1：粘性会话，在当前账号上等待
                            var maxWait = selectResult.WaitPlan.MaxConcurrency + 20;
                            if (!await concurrencyStrategy.IncrementWaitCountAsync(selectResult.AccountToken.Id, maxWait, context.RequestAborted))
                            {
                                throw new BadRequestException("等待队列已满，请稍后重试");
                            }

                            try
                            {
                                acquired = await concurrencyStrategy.WaitForSlotAsync(
                                    selectResult.AccountToken.Id,
                                    activeRequestId,
                                    selectResult.WaitPlan.MaxConcurrency,
                                    selectResult.WaitPlan.Timeout,
                                    context.RequestAborted);
                            }
                            finally
                            {
                                await concurrencyStrategy.DecrementWaitCountAsync(selectResult.AccountToken.Id, context.RequestAborted);
                            }

                            if (!acquired)
                            {
                                throw new BadRequestException($"账号 {selectResult.AccountToken.Name} 繁忙，请稍后重试");
                            }
                        }
                        else
                        {
                            // 场景2：非粘性会话，快速切换账号
                            shouldSwitchAccount = true;
                            break; // 跳出内层循环，执行切换逻辑
                        }
                    }

                    var accountedHandler = chatModelHandlerFactory.CreateHandler(
                        platform,
                        selectResult.AccountToken.AccessToken,
                        selectResult.AccountToken.BaseUrl,
                        selectResult.AccountToken.ExtraProperties,
                        selectResult.AllowOfficialClientMimic,
                        selectResult.AccountToken.ModelWhites,
                        selectResult.AccountToken.ModelMapping);

                    var upContext = await accountedHandler.ProcessRequestContextAsync(
                        downContext,
                        degradationLevel,
                        context.RequestAborted);

                    var startRecord = new UsageRecordStartItem(
                        UsageRecordId: activeRequestId,
                        CorrelationId: correlationId,
                        Platform: platform,
                        ApiKeyId: apiKeyId,
                        ApiKeyName: apiKeyName,
                        AccountTokenId: selectResult.AccountToken.Id,
                        AccountTokenName: selectResult.AccountToken.Name,
                        ProviderGroupId: selectResult.ProviderGroupId,
                        ProviderGroupName: selectResult.ProviderGroupName,
                        GroupRateMultiplier: selectResult.GroupRateMultiplier,
                        IsStreaming: downContext.IsStreaming,
                        DownRequestMethod: context.Request.Method,
                        DownRequestUrl: context.Request.GetDisplayUrl(),
                        DownModelId: downContext.ModelId,
                        DownClientIp: context.Connection.RemoteIpAddress?.ToString(),
                        DownUserAgent: context.Request.Headers.UserAgent.ToString(),
                        UpModelId: upContext.MappedModelId,
                        UpUserAgent: upContext.GetUserAgent(),
                        UpRequestUrl: upContext.GetFullUrl(),
                        DownRequestHeaders: CaptureHeaders(downContext.Headers),
                        UpRequestHeaders: CaptureHeaders(upContext.Headers)
                    );
                    if (_loggingOptions.IsBodyLoggingEnabled)
                    {
                        var bodyContent = upContext.BodyJson != null ? upContext.BodyJson.ToString() : null;
                        startRecord.LoggingBody(
                            downContext.IsMultipart ?
                                "[Multipart Data - Logging Skipped]" :
                                downContext.GetBodyPreview(_loggingOptions.MaxBodyLength),
                            string.IsNullOrEmpty(bodyContent) ?
                                string.Empty :
                                bodyContent.Length > _loggingOptions.MaxBodyLength ?
                                    bodyContent[.._loggingOptions.MaxBodyLength] + "...[Truncated]" :
                                    bodyContent);
                    }
                    usageRecordHostedService.TryEnqueue(startRecord);

                    var stopwatch = Stopwatch.StartNew();
                    int? httpStatusCode = null;
                    StreamForwardResult? forwardResult = null;
                    string? errorBody = null;
                    var usageStatus = UsageStatus.Success;
                    string? usageStatusDescription = null;
                    try
                    {
                        // 5. 执行 HTTP 请求
                        using var response = await accountedHandler.ProxyRequestAsync(upContext, context.RequestAborted);
                        stopwatch.Stop();
                        httpStatusCode = (int)response.StatusCode;
                        // 6. 判断响应状态
                        if (response.IsSuccessStatusCode)
                        {
                            usageStatus = UsageStatus.Success;
                            // ===== 成功路径 =====

                            // 处理成功结果：清除退避计数
                            await smartProxyAppService.HandleSuccessAsync(
                                selectResult.AccountToken.Id,
                                context.RequestAborted);

                            WriteResponseHeaders(context, (int)response.StatusCode, ExtractHeaders(response));

                            var options = new ForwardResponseOptions(_loggingOptions.IsBodyLoggingEnabled, _loggingOptions.MaxBodyLength, accountedHandler.GetSseLineCallback(upContext.SessionId));
                            forwardResult = await streamProcessor.ForwardResponseAsync(
                                response,
                                context.Response.Body,
                                accountedHandler,
                                downContext.IsStreaming,
                                options,
                                context.RequestAborted);
                            return;
                        }
                        else
                        {
                            usageStatus = UsageStatus.Failed;
                            // ===== 失败路径 =====
                            errorBody = await response.Content.ReadAsStringAsync(context.RequestAborted);
                            var responseHeaders = ExtractHeaders(response);

                            logger.LogWarning("账号请求失败，状态码: {StatusCode}，正在分析错误：{BodyContent}", httpStatusCode.Value, errorBody);

                            // 7. 错误分析
                            var errorAnalysis = await accountedHandler.AnalyzeErrorAsync(httpStatusCode.Value, responseHeaders, errorBody);

                            // 8. 决策逻辑
                            const int MaxSameAccountRetries = 3;
                            var instruction = DetermineFailureInstruction(errorAnalysis, currentAccountRetryCount, MaxSameAccountRetries);
                            var retryAfter = CalculateRetryDelay(errorAnalysis, currentAccountRetryCount, instruction);

                            // 9. 执行指令
                            switch (instruction)
                            {
                                case FailureInstruction.RetrySameAccount:
                                    currentAccountRetryCount++;
                                    if (errorAnalysis.RequiresDowngrade)
                                    {
                                        degradationLevel++;
                                        usageStatusDescription = $"启用降级级别 {degradationLevel} 进行重试 (Retry {currentAccountRetryCount})";
                                    }
                                    else
                                    {
                                        usageStatusDescription = $"同账号重试，延迟 {retryAfter.TotalMilliseconds}ms，重试次数: {currentAccountRetryCount}";
                                    }
                                    await Task.Delay(retryAfter, context.RequestAborted);
                                    break;

                                case FailureInstruction.SwitchAccount:
                                    // 切换账号前，更新账户状态（熔断）
                                    await smartProxyAppService.HandleFailureAsync(
                                        new HandleFailureInputDto(
                                            selectResult.AccountToken.Id,
                                            httpStatusCode.Value,
                                            errorBody,
                                            errorAnalysis,
                                            retryAfter),
                                        context.RequestAborted);

                                    if (selectResult.AvailableAccountCount <= 1)
                                    {
                                        usageStatusDescription = $"账号 '{selectResult.AccountToken.Name}' 不可用 (状态码: {httpStatusCode})，且无其他可用账号";
                                        throw new ServiceUnavailableException(usageStatusDescription);
                                    }
                                    shouldSwitchAccount = true;
                                    usageStatusDescription = $"账号 '{selectResult.AccountToken.Name}' 不可用 (状态码: {httpStatusCode})，切换到其他账号";
                                    break;

                                case FailureInstruction.Fail:
                                    // 彻底失败前，更新账户状态
                                    await smartProxyAppService.HandleFailureAsync(
                                        new HandleFailureInputDto(
                                            selectResult.AccountToken.Id,
                                            httpStatusCode.Value,
                                            errorBody,
                                            errorAnalysis,
                                            null),
                                        context.RequestAborted);

                                    usageStatusDescription = $"请求失败，不进行重试：{errorBody}";
                                    WriteResponseHeaders(context, httpStatusCode.Value, responseHeaders);
                                    await context.Response.WriteAsync(errorBody, context.RequestAborted);
                                    return;
                            }
                            if (!string.IsNullOrEmpty(usageStatusDescription))
                            {
                                logger.LogWarning(usageStatusDescription);
                            }
                        }
                    }
                    finally
                    {
                        await concurrencyStrategy.ReleaseSlotAsync(selectResult.AccountToken.Id, activeRequestId);
                        usageRecordHostedService.TryEnqueue(new UsageRecordEndItem(
                            UsageRecordId: activeRequestId,
                            Duration: stopwatch.ElapsedMilliseconds,
                            UpStatusCode: httpStatusCode,
                            Status: usageStatus,
                            StatusDescription: usageStatusDescription,
                            UpResponseBody: forwardResult?.CapturedBody ?? errorBody,
                            DownResponseBody: forwardResult?.CapturedBody ?? errorBody,
                            InputTokens: forwardResult?.Usage?.InputTokens,
                            OutputTokens: forwardResult?.Usage?.OutputTokens,
                            CacheReadTokens: forwardResult?.Usage?.CacheReadTokens,
                            CacheCreationTokens: forwardResult?.Usage?.CacheCreationTokens
                        ));
                    }
                }

                // 执行切换账号逻辑
                if (shouldSwitchAccount)
                {
                    excludedAccountIds.Add(selectResult.AccountToken.Id);
                    accountSwitchCount++;
                }

            } // End outer while
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "代理网关异常被拦截: {Message}", ex.Message);

            if (context.Response.HasStarted)
            {
                logger.LogWarning("代理网关异常：响应已开始，无法覆写平台[{Platform}]的专有错误格式", platform);
                return;
            }

            var formatter = errorFormatterFactory.GetFormatter(platform);
            var errorResponse = formatter.Format(ex);

            context.Response.StatusCode = errorResponse.StatusCode;
            context.Response.ContentType = errorResponse.ContentType;

            await context.Response.WriteAsync(errorResponse.Payload, System.Text.Encoding.UTF8, context.RequestAborted);
        }
    }

    private static (ProviderPlatform Platform, Guid ApiKeyId, string ApiKeyName) ValidateAndGetContext(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<PlatformMetadata>();
        if (metadata == null) throw new NotFoundException("平台路由未配置");

        var apiKeyIdClaim = context.User.FindFirst(AuthenticationConstants.ApiKeyIdClaimType);
        var apiKeyNameClaim = context.User.FindFirst(AuthenticationConstants.ApiKeyNameClaimType);

        if (apiKeyIdClaim == null || !Guid.TryParse(apiKeyIdClaim.Value, out var apiKeyId) ||
            apiKeyNameClaim == null)
        {
            throw new UnauthorizedException("请求未经认证");
        }

        return (metadata.Platform, apiKeyId, apiKeyNameClaim.Value);
    }


    private static bool IsHopByHopHeader(string headerName) => headerName.ToLowerInvariant() switch
    {
        "connection" or "keep-alive" or "transfer-encoding" or "te" or "trailer" or "proxy-authorization" or "proxy-authenticate" or "upgrade" or "expect" or "proxy-connection" => true,
        _ => false
    };

    private static Dictionary<string, IEnumerable<string>> ExtractHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, IEnumerable<string>>();
        foreach (var header in response.Headers)
            headers[header.Key] = header.Value;
        if (response.Content?.Headers != null)
            foreach (var header in response.Content.Headers)
                headers[header.Key] = header.Value;
        return headers;
    }

    private static void WriteResponseHeaders(HttpContext context, int statusCode, Dictionary<string, IEnumerable<string>> headers)
    {
        context.Response.StatusCode = statusCode;
        foreach (var header in headers)
        {
            if (!IsHopByHopHeader(header.Key))
                context.Response.Headers.Append(header.Key, new Microsoft.Extensions.Primitives.StringValues(header.Value.ToArray()));
        }
    }

    private static FailureInstruction DetermineFailureInstruction(
        ModelErrorAnalysisResult errorAnalysis,
        int currentRetryCount,
        int maxRetries)
    {
        if (errorAnalysis.IsRetryableOnSameAccount && currentRetryCount < maxRetries)
        {
            return FailureInstruction.RetrySameAccount;
        }

        if (errorAnalysis.ErrorType == ModelErrorType.RateLimit ||
            errorAnalysis.ErrorType == ModelErrorType.SignatureError ||
            errorAnalysis.ErrorType == ModelErrorType.ServerError ||
            errorAnalysis.ErrorType == ModelErrorType.AuthenticationError)
        {
            return FailureInstruction.SwitchAccount;
        }

        return FailureInstruction.Fail;
    }

    private static TimeSpan CalculateRetryDelay(
        ModelErrorAnalysisResult errorAnalysis,
        int currentRetryCount,
        FailureInstruction instruction)
    {
        if (instruction != FailureInstruction.RetrySameAccount)
        {
            return TimeSpan.Zero;
        }

        if (errorAnalysis.RetryAfter.HasValue)
        {
            return errorAnalysis.RetryAfter.Value;
        }

        var delayMs = 1000 * Math.Pow(2, currentRetryCount);
        var jitter = Random.Shared.NextDouble() * 0.4 + 0.8;
        return TimeSpan.FromMilliseconds(delayMs * jitter);
    }

    private static string? CaptureHeaders(Dictionary<string, string> headers)
    {
        var filtered = headers
            .Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
                       !h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
            .Select(h => $"{h.Key}: {h.Value}");

        return string.Join("\n", filtered);
    }

}

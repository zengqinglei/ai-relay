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

        // 请求级变量（提升到外层，供三段式写入使用）
        var usageRecordId = Guid.CreateVersion7();
        var attemptNumber = 0;
        var overallStopwatch = Stopwatch.StartNew();
        var finalStatus = UsageStatus.Failed;
        string? finalStatusDescription = null;
        StreamForwardResult? finalForwardResult = null;
        string? downResponseBody = null;
        // 最终成功尝试的账号信息（用于定价）
        string? lastUpModelId = null;
        Guid? lastAccountTokenId = null;

        // 下游请求 body（在入队 StartItem 前捕获，日志级别为 body logging）
        var downRequestBody = _loggingOptions.IsBodyLoggingEnabled
            ? (downContext.IsMultipart
                ? "[Multipart Data - Logging Skipped]"
                : downContext.GetBodyPreview(_loggingOptions.MaxBodyLength))
            : null;
        var downRequestHeaders = _loggingOptions.IsBodyLoggingEnabled ? CaptureHeaders(downContext.Headers) : null;

        // Step 1: 入队 StartItem（INSERT UsageRecord，Status=InProgress 立即可见）
        usageRecordHostedService.TryEnqueue(new UsageRecordStartItem(
            UsageRecordId: usageRecordId,
            CorrelationId: correlationId,
            Platform: platform,
            ApiKeyId: apiKeyId,
            ApiKeyName: apiKeyName,
            IsStreaming: downContext.IsStreaming,
            DownRequestMethod: context.Request.Method,
            DownRequestUrl: context.Request.GetDisplayUrl(),
            DownModelId: downContext.ModelId,
            DownClientIp: context.Connection.RemoteIpAddress?.ToString(),
            DownUserAgent: context.Request.Headers.UserAgent.ToString(),
            DownRequestHeaders: downRequestHeaders,
            DownRequestBody: downRequestBody
        ));

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

                var requiresFingerprint = selectResult.AccountToken.AllowOfficialClientMimic;

                if (requiresFingerprint)
                {
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
                var degradationLevel = 0;

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
                            shouldSwitchAccount = true;
                            break;
                        }
                    }

                    var accountedHandler = chatModelHandlerFactory.CreateHandler(
                        platform,
                        selectResult.AccountToken.AccessToken,
                        selectResult.AccountToken.BaseUrl,
                        selectResult.AccountToken.ExtraProperties,
                        selectResult.AccountToken.AllowOfficialClientMimic,
                        selectResult.AccountToken.ModelWhites,
                        selectResult.AccountToken.ModelMapping);

                    var upContext = await accountedHandler.ProcessRequestContextAsync(
                        downContext,
                        degradationLevel,
                        context.RequestAborted);

                    var attemptStopwatch = Stopwatch.StartNew();
                    attemptNumber++;
                    int? httpStatusCode = null;
                    StreamForwardResult? forwardResult = null;
                    string? errorBody = null;
                    var attemptStatus = UsageStatus.Failed;
                    string? attemptStatusDesc = null;
                    string? upResponseBody = null;

                    // Step 2: 入队 AttemptStartItem（INSERT Attempt，Status=InProgress，立即可见当前账号）
                    usageRecordHostedService.TryEnqueue(new UsageRecordAttemptStartItem(
                        UsageRecordId: usageRecordId,
                        AttemptNumber: attemptNumber,
                        AccountTokenId: selectResult.AccountToken.Id,
                        AccountTokenName: selectResult.AccountToken.Name,
                        ProviderGroupId: selectResult.ProviderGroupId,
                        ProviderGroupName: selectResult.ProviderGroupName,
                        GroupRateMultiplier: selectResult.GroupRateMultiplier,
                        UpModelId: upContext.MappedModelId,
                        UpUserAgent: upContext.GetUserAgent(),
                        UpRequestUrl: upContext.GetFullUrl(),
                        UpRequestHeaders: _loggingOptions.IsBodyLoggingEnabled ? CaptureHeaders(upContext.Headers) : null,
                        UpRequestBody: _loggingOptions.IsBodyLoggingEnabled ? (upContext.BodyJson != null ? upContext.BodyJson.ToString() : null) : null
                    ));

                    try
                    {
                        // 5. 执行 HTTP 请求
                        using var response = await accountedHandler.ProxyRequestAsync(upContext, context.RequestAborted);
                        attemptStopwatch.Stop();
                        httpStatusCode = (int)response.StatusCode;

                        if (response.IsSuccessStatusCode)
                        {
                            attemptStatus = UsageStatus.Success;
                            finalStatus = UsageStatus.Success;

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

                            finalForwardResult = forwardResult;
                            downResponseBody = _loggingOptions.IsBodyLoggingEnabled ? forwardResult?.CapturedBody : null;

                            // 记录最终成功尝试的账号信息（供 EndItem 定价使用）
                            lastUpModelId = upContext.MappedModelId;
                            lastAccountTokenId = selectResult.AccountToken.Id;

                            return;
                        }
                        else
                        {
                            attemptStatus = UsageStatus.Failed;
                            errorBody = await response.Content.ReadAsStringAsync(context.RequestAborted);
                            if (_loggingOptions.IsBodyLoggingEnabled) upResponseBody = errorBody;
                            var responseHeaders = ExtractHeaders(response);

                            logger.LogWarning("账号请求失败，状态码: {StatusCode}，正在分析错误：{BodyContent}", httpStatusCode.Value, errorBody);

                            var errorAnalysis = await accountedHandler.AnalyzeErrorAsync(httpStatusCode.Value, responseHeaders, errorBody);

                            const int MaxSameAccountRetries = 3;
                            var instruction = DetermineFailureInstruction(errorAnalysis, currentAccountRetryCount, MaxSameAccountRetries);
                            var retryAfter = CalculateRetryDelay(errorAnalysis, currentAccountRetryCount, instruction);

                            switch (instruction)
                            {
                                case FailureInstruction.RetrySameAccount:
                                    currentAccountRetryCount++;
                                    if (errorAnalysis.RequiresDowngrade)
                                    {
                                        degradationLevel++;
                                        attemptStatusDesc = $"启用降级级别 {degradationLevel} 进行重试 (Retry {currentAccountRetryCount})";
                                    }
                                    else
                                    {
                                        attemptStatusDesc = $"同账号重试，延迟 {retryAfter.TotalMilliseconds}ms，重试次数: {currentAccountRetryCount}";
                                    }
                                    await Task.Delay(retryAfter, context.RequestAborted);
                                    break;

                                case FailureInstruction.SwitchAccount:
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
                                        attemptStatusDesc = $"账号 '{selectResult.AccountToken.Name}' 不可用 (状态码: {httpStatusCode})，且无其他可用账号";
                                        throw new ServiceUnavailableException(attemptStatusDesc);
                                    }
                                    shouldSwitchAccount = true;
                                    attemptStatusDesc = $"账号 '{selectResult.AccountToken.Name}' 不可用 (状态码: {httpStatusCode})，切换到其他账号";
                                    break;

                                case FailureInstruction.Fail:
                                    await smartProxyAppService.HandleFailureAsync(
                                        new HandleFailureInputDto(
                                            selectResult.AccountToken.Id,
                                            httpStatusCode.Value,
                                            errorBody,
                                            errorAnalysis,
                                            null),
                                        context.RequestAborted);

                                    attemptStatusDesc = $"请求失败，不进行重试：{errorBody}";
                                    WriteResponseHeaders(context, httpStatusCode.Value, responseHeaders);
                                    await context.Response.WriteAsync(errorBody, context.RequestAborted);
                                    return;
                            }

                            if (!string.IsNullOrEmpty(attemptStatusDesc))
                            {
                                logger.LogWarning(attemptStatusDesc);
                            }
                        }
                    }
                    finally
                    {
                        attemptStopwatch.Stop();
                        await concurrencyStrategy.ReleaseSlotAsync(selectResult.AccountToken.Id, activeRequestId);

                        // Step 3: 入队 AttemptEndItem（UPDATE Attempt 为最终状态）
                        usageRecordHostedService.TryEnqueue(new UsageRecordAttemptEndItem(
                            UsageRecordId: usageRecordId,
                            AttemptNumber: attemptNumber,
                            UpStatusCode: httpStatusCode,
                            DurationMs: attemptStopwatch.ElapsedMilliseconds,
                            Status: attemptStatus,
                            StatusDescription: attemptStatusDesc,
                            UpResponseBody: _loggingOptions.IsBodyLoggingEnabled ? upResponseBody : null
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
            finalStatusDescription = ex.Message;
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
        finally
        {
            overallStopwatch.Stop();

            // Step 4: 入队 EndItem（UPDATE UsageRecord 为最终状态）
            usageRecordHostedService.TryEnqueue(new UsageRecordEndItem(
                UsageRecordId: usageRecordId,
                Duration: overallStopwatch.ElapsedMilliseconds,
                Status: finalStatus,
                StatusDescription: finalStatusDescription,
                DownResponseBody: downResponseBody,
                InputTokens: finalForwardResult?.Usage?.InputTokens,
                OutputTokens: finalForwardResult?.Usage?.OutputTokens,
                CacheReadTokens: finalForwardResult?.Usage?.CacheReadTokens,
                CacheCreationTokens: finalForwardResult?.Usage?.CacheCreationTokens,
                AttemptCount: attemptNumber
            ));
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

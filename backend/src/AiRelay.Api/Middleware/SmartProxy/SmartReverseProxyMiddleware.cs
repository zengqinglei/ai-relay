using AiRelay.Api.Authentication;
using AiRelay.Api.HostedServices.Workers;
using AiRelay.Api.Middleware.SmartProxy.ErrorHandling;
using AiRelay.Application.ProviderAccounts.AppServices;
using AiRelay.Application.ProviderGroups.AppServices;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.ProviderGroups.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Helpers;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.UsageRecords.Options;
using Leistd.Exception.Core;
using Leistd.Tracing.Core.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using System.Text;

namespace AiRelay.Api.Middleware.SmartProxy;

public class SmartReverseProxyMiddleware(
    ISmartProxyAppService smartProxyAppService,
    IChatModelHandlerFactory chatModelHandlerFactory,
    ProxyErrorFormatterFactory errorFormatterFactory,
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
        var (routeProfile, apiKeyId, apiKeyName) = ValidateAndGetContext(context);
        var correlationId = correlationIdProvider.Get() ?? correlationIdProvider.Create();

        var chatModelHandler = chatModelHandlerFactory.CreateHandler(routeProfile);
        var downContext = await ProcessDownstreamRequestAsync(context, routeProfile, chatModelHandler, apiKeyId);

        // 请求级变量
        var usageRecordId = Guid.CreateVersion7();
        var attemptNumber = 0;
        var overallStopwatch = Stopwatch.StartNew();
        var finalStatus = UsageStatus.Failed;
        string? finalStatusDescription = null;
        string? downResponseBody = null;
        ResponseUsage? finalUsage = null;

        var downRequestBody = _loggingOptions.IsBodyLoggingEnabled
            ? (downContext.IsMultipart
                ? "[Multipart Data - Logging Skipped]"
                : downContext.PreloadedBodyPreview)
            : null;
        var downRequestHeaders = _loggingOptions.IsBodyLoggingEnabled ? CaptureHeaders(downContext.Headers) : null;

        // Step 1: 入队 StartItem
        usageRecordHostedService.TryEnqueue(new UsageRecordStartItem(
            UsageRecordId: usageRecordId,
            CorrelationId: correlationId,
            SessionId: downContext.SessionId,
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
            const int MaxAccountSwitches = 5;

            while (true)
            {
                if (accountSwitchCount >= MaxAccountSwitches)
                {
                    throw new ServiceUnavailableException($"已尝试 {MaxAccountSwitches} 个账号，均不可用");
                }

                // 1. 选号
                var selectResult = await smartProxyAppService.SelectAccountAsync(
                        new SelectProxyAccountInputDto
                        {
                            ApiKeyId = apiKeyId,
                            ApiKeyName = apiKeyName,
                            SessionHash = downContext.SessionId,
                            ExcludedAccountIds = excludedAccountIds,
                            ModelId = downContext.ModelId,
                            AllowedCombinations = RouteProfileRegistry.Profiles.TryGetValue(routeProfile, out var profileDef)
                                ? profileDef.SupportedCombinations
                                : null
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

                var currentAccountRetryCount = 1;
                var shouldSwitchAccount = false;
                var degradationLevel = 0;

                // BackoffCount=0: 3次；=1: 2次；≥2: 1次（账号历史失败越多，给予越少重试机会）
                var maxSameAccountRetries = selectResult.BackoffCount switch
                {
                    0 => 3,
                    1 => 2,
                    _ => 1
                };

                // 内层循环：同账号重试
                while (!shouldSwitchAccount)
                {
                    // 并发熔断感知：若账号已被并发请求触发熔断，立即切换，避免继续消耗重试次数
                    if (await smartProxyAppService.IsRateLimitedAsync(selectResult.AccountToken.Id, context.RequestAborted))
                    {
                        shouldSwitchAccount = true;
                        logger.LogDebug("账号 {AccountName} 已被并发请求触发熔断，跳过重试直接切换账号", selectResult.AccountToken.Name);
                        break;
                    }

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
                                throw new ServiceUnavailableException("等待队列已满，请稍后重试");
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
                                throw new ServiceUnavailableException($"账号 {selectResult.AccountToken.Name} 繁忙，请稍后重试");
                            }
                        }
                        else
                        {
                            shouldSwitchAccount = true;
                            break;
                        }
                    }

                    var accountedHandler = chatModelHandlerFactory.CreateHandler(
                        selectResult.AccountToken.Provider,
                        selectResult.AccountToken.AuthMethod,
                        selectResult.AccountToken.AccessToken!,
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
                    var attemptStatus = UsageStatus.Failed;
                    string? attemptStatusDesc = null;
                    string? upResponseBody = null;

                    // Step 2: 入队 AttemptStartItem
                    var upRequestBody = _loggingOptions.IsBodyLoggingEnabled
                        ? (downContext.IsMultipart
                            ? "[Multipart Data - Logging Skipped]"
                            : upContext.GetBodyPreview(downContext.PreloadedBodyPreview, _loggingOptions.MaxBodyLength))
                        : null;
                    usageRecordHostedService.TryEnqueue(new UsageRecordAttemptStartItem(
                        UsageRecordId: usageRecordId,
                        AttemptNumber: attemptNumber,
                        AccountTokenId: selectResult.AccountToken.Id,
                        AccountTokenName: selectResult.AccountToken.Name,
                        Provider: selectResult.AccountToken.Provider,
                        AuthMethod: selectResult.AccountToken.AuthMethod,
                        ProviderGroupId: selectResult.ProviderGroupId,
                        ProviderGroupName: selectResult.ProviderGroupName,
                        GroupRateMultiplier: selectResult.GroupRateMultiplier,
                        UpModelId: upContext.MappedModelId,
                        UpUserAgent: upContext.GetUserAgent(),
                        UpRequestUrl: upContext.GetFullUrl(),
                        UpRequestHeaders: _loggingOptions.IsBodyLoggingEnabled ? CaptureHeaders(upContext.Headers) : null,
                        UpRequestBody: upRequestBody
                    ));

                    try
                    {
                        // Phase 1: 发送请求，等待响应头（body 未消费）
                        var proxyResponse = await accountedHandler.SendChatRequestAsync(upContext, downContext, downContext.IsStreaming, context.RequestAborted);
                        httpStatusCode = proxyResponse.StatusCode;

                        bool isStreamCrash = false;

                        // ====================================================================
                        // ── 第一阶段：处理正常响应（包含流读首包崩溃检查）
                        // ====================================================================
                        if (proxyResponse.IsSuccess)
                        {
                            // ── Phase 2: 成功响应 - 写入下游 Headers，然后转发事件流 ──
                            attemptStatus = UsageStatus.Success;
                            finalStatus = UsageStatus.Success;

                            bool isCheckStreamHealth = downContext.IsStreaming && selectResult.AccountToken.IsCheckStreamHealth;
                            bool headersWritten = false;
                            var bufferedBytes = new List<byte[]>();

                            var tempUpResponseBody = new StringBuilder();
                            var tempDownResponseBody = new StringBuilder();

                            try
                            {
                                if (!isCheckStreamHealth)
                                {
                                    // 未开启健康检查，或者非流请求，原样立即发送Headers
                                    await smartProxyAppService.HandleSuccessAsync(selectResult.AccountToken.Id, context.RequestAborted);
                                    WriteResponseHeaders(context, proxyResponse.StatusCode, proxyResponse.Headers);
                                    headersWritten = true;
                                }

                                // 枚举事件流（ConvertedBytes ?? OriginalBytes 直接透传给下游）
                                await foreach (var evt in proxyResponse.Events!.WithCancellation(context.RequestAborted))
                                {
                                    // 收集 Usage
                                    if (evt.IsComplete && evt.Usage != null)
                                    {
                                        finalUsage = evt.Usage;
                                    }

                                    if (isCheckStreamHealth && !headersWritten)
                                    {
                                        if (evt.Type == StreamEventType.Error)
                                        {
                                            isStreamCrash = true;
                                            attemptStatusDesc = $"流健康检查到内部错误事件节点 '{evt.Content ?? "unknown"}'";
                                            break;
                                        }

                                        if (evt.HasOutput)
                                        {
                                            // 实际输出产生，解除等待并放行
                                            await smartProxyAppService.HandleSuccessAsync(selectResult.AccountToken.Id, context.RequestAborted);
                                            WriteResponseHeaders(context, proxyResponse.StatusCode, proxyResponse.Headers);
                                            headersWritten = true;

                                            foreach (var chunk in bufferedBytes)
                                            {
                                                // 缓冲字节真实写入下游时，补记 tempDownResponseBody
                                                if (_loggingOptions.IsBodyLoggingEnabled &&
                                                    tempDownResponseBody.Length < _loggingOptions.MaxBodyLength)
                                                {
                                                    var maxLength = _loggingOptions.MaxBodyLength - tempDownResponseBody.Length;
                                                    var length = Math.Min(chunk.Length, maxLength);
                                                    tempDownResponseBody.Append(Encoding.UTF8.GetString(chunk[..length]));
                                                    if (chunk.Length > maxLength) tempDownResponseBody.Append("...[Truncated]");
                                                }
                                                await context.Response.Body.WriteAsync(chunk, context.RequestAborted);
                                            }
                                            bufferedBytes.Clear();
                                            await context.Response.Body.FlushAsync(context.RequestAborted);
                                        }
                                    }

                                    // 转发字节
                                    var bytesToForward = evt.ConvertedBytes ?? evt.OriginalBytes;

                                    if (bytesToForward != null)
                                    {
                                        // 上游原始数据：只要收到就记录，不受健康检查状态限制
                                        if (_loggingOptions.IsBodyLoggingEnabled &&
                                            evt.OriginalBytes != null &&
                                            tempUpResponseBody.Length < _loggingOptions.MaxBodyLength)
                                        {
                                            var maxLength = _loggingOptions.MaxBodyLength - tempUpResponseBody.Length;
                                            var length = Math.Min(evt.OriginalBytes.Length, maxLength);
                                            tempUpResponseBody.Append(Encoding.UTF8.GetString(evt.OriginalBytes[..length]));
                                            if (evt.OriginalBytes.Length > maxLength)
                                                tempUpResponseBody.Append("...[Truncated]");
                                        }

                                        // 决定缓冲还是直接写入
                                        if (isCheckStreamHealth && !headersWritten)
                                        {
                                            bufferedBytes.Add(bytesToForward);
                                        }
                                        else
                                        {
                                            // 下游转发数据：只在真实写入时记录
                                            if (_loggingOptions.IsBodyLoggingEnabled &&
                                                tempDownResponseBody.Length < _loggingOptions.MaxBodyLength)
                                            {
                                                var maxLength = _loggingOptions.MaxBodyLength - tempDownResponseBody.Length;
                                                var length = Math.Min(bytesToForward.Length, maxLength);
                                                tempDownResponseBody.Append(Encoding.UTF8.GetString(bytesToForward[..length]));
                                                if (bytesToForward.Length > maxLength)
                                                    tempDownResponseBody.Append("...[Truncated]");
                                            }

                                            await context.Response.Body.WriteAsync(bytesToForward, context.RequestAborted);
                                            await context.Response.Body.FlushAsync(context.RequestAborted);
                                        }
                                    }
                                }

                                if (isCheckStreamHealth && !headersWritten && !isStreamCrash)
                                {
                                    isStreamCrash = true;
                                    attemptStatusDesc = "流健康检查未读取到包含有效文本，判定为空流或无响应";
                                }

                                if (isStreamCrash)
                                {
                                    context.Response.Clear();
                                    logger.LogWarning(attemptStatusDesc);
                                    // 重点：不要 return，让它自然走到下面的 Phase 2 进行切号与重试
                                }
                                else
                                {
                                    // 完全成功，直接返回退出当前请求
                                    return;
                                }
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                attemptStatus = UsageStatus.Failed;
                                finalStatus = UsageStatus.Failed;
                                if (context.Response.HasStarted)
                                {
                                    // 标记为中途坠机，避免记录成 Success
                                    attemptStatusDesc = ex is IOException
                                        ? $"上游流传输中途断开（已向下游转发部分数据）: {ex.Message}"
                                        : $"流转发过程中发生意外异常: {ex.Message}";

                                    // 响应头或部分实体已发出，无法补救，只能向上抛出交由最外层强制 Abort 断开网络
                                    throw;
                                }
                                else
                                {
                                    // ⭐ 尚未发送任何响应内容（仅仅在 WriteResponseHeaders 写入了内存头，但未 Flush）
                                    // 清理掉准备发给下游的 HTTP Headers，将其回滚到干净状态
                                    context.Response.Clear();

                                    // 标记流崩溃标志，使其跃迁出成功区块，平滑下落到异常重试处理分支
                                    isStreamCrash = true;
                                    attemptStatusDesc = ex is IOException
                                        ? $"上游流在首包前断开，触发同号重试或切号重试机制: {ex.Message}"
                                        : $"流尚未开始下发便中断异常，触发同号重试或切号重试机制: {ex.Message}";
                                    logger.LogWarning(ex, attemptStatusDesc);
                                }
                            }
                            finally
                            {
                                // 确保无论是用户中途取消(Cancel)、转发异常中断，还是正常完成，
                                // 都将这段时间真正接收到并下发的片段保存至日志审计中，避免流失。
                                upResponseBody = tempUpResponseBody.ToString();
                                downResponseBody = tempDownResponseBody.ToString();
                            }
                        }

                        // ====================================================================
                        // ── 第二阶段：错误响应（或流读首包崩溃），进入重试决策
                        // ====================================================================
                        if (!proxyResponse.IsSuccess || isStreamCrash)
                        {
                            if (!isStreamCrash)
                            {
                                attemptStatus = UsageStatus.Failed;
                                upResponseBody = LoggingSubBody(proxyResponse.ErrorBody);
                            }

                            logger.LogWarning("账号请求失败或流首包崩溃，状态码: {StatusCode}，正在分析错误：{BodyContent}", httpStatusCode, upResponseBody);

                            var retryPolicy = isStreamCrash ?
                                new ModelErrorAnalysisResult() { IsCanRetry = true } : // 对于流直接断开导致的崩溃，强制视为网络异常要求重试（等效于502/504等）
                                await accountedHandler.CheckRetryPolicyAsync(httpStatusCode.Value, proxyResponse.Headers, proxyResponse.ErrorBody);

                            var (instruction, retryAfter) = DetermineFailureInstruction(retryPolicy, currentAccountRetryCount, maxSameAccountRetries);

                            switch (instruction)
                            {
                                case FailureInstruction.RetrySameAccount:
                                    if (retryPolicy.RequiresDowngrade)
                                    {
                                        degradationLevel++;
                                        attemptStatusDesc = $"启用降级级别 {degradationLevel} 进行重试 (Retry {currentAccountRetryCount}){(attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "")}";
                                    }
                                    else
                                    {
                                        attemptStatusDesc = $"同账号重试，延迟 {retryAfter.TotalMilliseconds}ms，重试次数: {currentAccountRetryCount}{(attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "")}";
                                    }
                                    await Task.Delay(retryAfter, context.RequestAborted);
                                    currentAccountRetryCount++;
                                    break;

                                case FailureInstruction.SwitchAccount:
                                    await smartProxyAppService.HandleFailureAsync(
                                        new HandleFailureInputDto(
                                            selectResult.AccountToken.Id,
                                            httpStatusCode.Value,
                                            proxyResponse.ErrorBody,
                                            retryPolicy),
                                        context.RequestAborted);

                                    if (selectResult.AvailableAccountCount <= 1)
                                    {
                                        attemptStatusDesc = $"账号 '{selectResult.AccountToken.Name}' 不可用 (状态码: {httpStatusCode})，且无其他可用账号{(attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "")}";
                                        throw new ServiceUnavailableException(attemptStatusDesc);
                                    }
                                    shouldSwitchAccount = true;
                                    attemptStatusDesc = $"账号 '{selectResult.AccountToken.Name}' 不可用 (状态码: {httpStatusCode})，切换到其他账号{(attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "")}";
                                    break;

                                case FailureInstruction.Fail:
                                    await smartProxyAppService.HandleFailureAsync(
                                        new HandleFailureInputDto(
                                            selectResult.AccountToken.Id,
                                            httpStatusCode.Value,
                                            proxyResponse.ErrorBody,
                                            retryPolicy),
                                        context.RequestAborted);

                                    attemptStatusDesc = $"请求失败，不进行重试：{upResponseBody}";
                                    WriteResponseHeaders(context, httpStatusCode.Value, proxyResponse.Headers);
                                    downResponseBody = LoggingSubBody(proxyResponse.ErrorBody);
                                    await context.Response.WriteAsync(proxyResponse.ErrorBody ?? "", context.RequestAborted);
                                    return;
                            }

                            if (!string.IsNullOrEmpty(attemptStatusDesc))
                                logger.LogWarning(attemptStatusDesc);
                        }
                    }
                    finally
                    {
                        attemptStopwatch.Stop();
                        await concurrencyStrategy.ReleaseSlotAsync(selectResult.AccountToken.Id, activeRequestId);

                        // Step 3: 入队 AttemptEndItem
                        usageRecordHostedService.TryEnqueue(new UsageRecordAttemptEndItem(
                            UsageRecordId: usageRecordId,
                            AttemptNumber: attemptNumber,
                            UpStatusCode: httpStatusCode,
                            DurationMs: attemptStopwatch.ElapsedMilliseconds,
                            Status: attemptStatus,
                            StatusDescription: attemptStatusDesc,
                            UpResponseBody: upResponseBody
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
        catch (OperationCanceledException)
        {
            finalStatus = UsageStatus.Failed;
            finalStatusDescription = "客户端主动断开连接";
            logger.LogInformation("原始客户端已强制断开连接，提前终止代理。");
        }
        catch (Exception ex)
        {
            finalStatus = UsageStatus.Failed;
            if (context.Response.HasStarted)
            {
                finalStatusDescription = ex is IOException
                    ? $"上游流传输中途断开: {ex.Message}"
                    : $"代理转发过程中发生意外异常: {ex.Message}";
                logger.LogWarning(ex, finalStatusDescription);

                // 模拟直连：遇到上游中途断开，不伪造 [DONE] 平滑结束，
                // 而是直接强行重置/释放当前 HttpContext 的网络连接。
                // 这样下游的官方 SDK (如 OpenAI/Claude Client) 会收到类似于 ECONNRESET 等网络层异常，
                // 从而能正确触发这些 SDK 自身内置的自动重试容灾机制。
                context.Abort();
                return;
            }
            else
            {
                finalStatusDescription = $"代理网关异常被拦截: {ex.Message}";
                logger.LogWarning(ex, finalStatusDescription);
            }

            var formatter = errorFormatterFactory.GetFormatter(routeProfile);
            var errorResponse = formatter.Format(ex);
            downResponseBody = LoggingSubBody(errorResponse.Payload);

            context.Response.StatusCode = errorResponse.StatusCode;
            context.Response.ContentType = errorResponse.ContentType;
            await context.Response.WriteAsync(errorResponse.Payload, Encoding.UTF8, context.RequestAborted);
        }
        finally
        {
            overallStopwatch.Stop();

            // Step 4: 入队 EndItem
            usageRecordHostedService.TryEnqueue(new UsageRecordEndItem(
                UsageRecordId: usageRecordId,
                Duration: overallStopwatch.ElapsedMilliseconds,
                Status: finalStatus,
                StatusDescription: finalStatusDescription,
                DownResponseBody: downResponseBody,
                InputTokens: finalUsage?.InputTokens,
                OutputTokens: finalUsage?.OutputTokens,
                CacheReadTokens: finalUsage?.CacheReadTokens,
                CacheCreationTokens: finalUsage?.CacheCreationTokens,
                AttemptCount: attemptNumber,
                DownStatusCode: context.Response.StatusCode
            ));
        }
    }

    private string? LoggingSubBody(string? sourceStr)
    {
        if (string.IsNullOrEmpty(sourceStr))
        {
            return sourceStr;
        }
        if (!_loggingOptions.IsBodyLoggingEnabled)
        {
            return null;
        }
        var length = Math.Min(sourceStr.Length, _loggingOptions.MaxBodyLength);
        var content = sourceStr[..length];

        return sourceStr.Length > _loggingOptions.MaxBodyLength ? content + "...[Truncated]" : content;
    }

    private async Task<DownRequestContext> ProcessDownstreamRequestAsync(HttpContext context, RouteProfile routeProfile, IChatModelHandler chatModelHandler, Guid apiKeyId)
    {
        var request = context.Request;
        var contentType = request.ContentType ?? "";
        var isMultipart = contentType.Contains("multipart", StringComparison.OrdinalIgnoreCase);

        const long MaxBodySize = 100 * 1024 * 1024; // 100MB
        bool hasBody = request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding");
        if (hasBody && !isMultipart)
        {
            // 对于已知长度的请求，直接检查
            if (request.ContentLength > MaxBodySize)
                throw new BadRequestException($"Request body too large, limit is {MaxBodySize / (1024 * 1024)}MB");

            // 启用流缓冲（实现基于内存+文件的分级缓存，消灭 LOH 分配）
            // 设置阈值为 MaxBodySize，超过此大小 request.Body.Read 阶段会抛出异常或停止缓存
            request.EnableBuffering(MaxBodySize);
        }

        var pathPrefix = RouteProfileRegistry.Profiles.TryGetValue(routeProfile, out var profileDef)
            ? profileDef.PathPrefix
            : string.Empty;

        var relativePath = pathPrefix;
        if (context.Request.RouteValues.TryGetValue("catch-all", out var catchAll) && catchAll != null)
        {
            var catchAllPath = catchAll.ToString()!;
            // 根据 PathPrefix 是否包含冒号来决定分隔符（冒号分隔用于 Google 特殊路径格式）
            var separator = pathPrefix.Contains(':') ? ":" : "/";
            relativePath = string.IsNullOrEmpty(catchAllPath)
                ? pathPrefix
                : $"{pathPrefix}{separator}{catchAllPath}";
        }

        var rawStream = (hasBody && !isMultipart) ? request.Body : null;
        var (extractedProps, bodyPreview) = await JsonExtractHelper.ExtractEssentialPropsAsync(rawStream, _loggingOptions.IsBodyLoggingEnabled, _loggingOptions.MaxBodyLength);

        var downContext = new DownRequestContext
        {
            Method = ParseHttpMethod(request.Method),
            RelativePath = relativePath,
            QueryString = request.QueryString.Value,
            Headers = ConvertHeaders(request.Headers),
            RawStream = rawStream,
            IsMultipart = isMultipart,
            ExtractedProps = extractedProps,
            PreloadedBodyPreview = bodyPreview
        };

        // 各平台 Handler 负责从 Header/Body 提取元信息（ModelId、SessionHash）
        chatModelHandler.ExtractModelInfo(downContext, apiKeyId);
        return downContext;
    }

    private static HttpMethod ParseHttpMethod(string method) => method?.ToUpperInvariant() switch
    {
        "GET" => HttpMethod.Get,
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "DELETE" => HttpMethod.Delete,
        "PATCH" => HttpMethod.Patch,
        "HEAD" => HttpMethod.Head,
        "OPTIONS" => HttpMethod.Options,
        _ => HttpMethod.Post
    };

    private static Dictionary<string, string> ConvertHeaders(IHeaderDictionary headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers) result[header.Key] = header.Value.ToString();
        return result;
    }

    private static (RouteProfile Profile, Guid ApiKeyId, string ApiKeyName) ValidateAndGetContext(HttpContext context)
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

        return (metadata.Profile, apiKeyId, apiKeyNameClaim.Value);
    }


    private static bool IsHopByHopHeader(string headerName) => headerName.ToLowerInvariant() switch
    {
        "connection" or "keep-alive" or "transfer-encoding" or "te" or "trailer" or "proxy-authorization" or "proxy-authenticate" or "upgrade" or "expect" or "proxy-connection" => true,
        _ => false
    };



    private static void WriteResponseHeaders(HttpContext context, int statusCode, Dictionary<string, IEnumerable<string>> headers)
    {
        context.Response.StatusCode = statusCode;
        foreach (var header in headers)
        {
            if (!IsHopByHopHeader(header.Key))
                context.Response.Headers.Append(header.Key, new StringValues(header.Value.ToArray()));
        }
    }

    private static (FailureInstruction Instruction, TimeSpan RetryDelay) DetermineFailureInstruction(
        ModelErrorAnalysisResult retryPolicy,
        int currentRetryCount,
        int maxRetries)
    {
        if (retryPolicy.IsCanRetry && currentRetryCount < maxRetries)
        {
            if (retryPolicy.RetryAfter.HasValue && retryPolicy.RetryAfter.Value.TotalSeconds >= 15)
                return (FailureInstruction.SwitchAccount, TimeSpan.Zero);

            var delay = retryPolicy.RetryAfter ?? TimeSpan.FromMilliseconds(1000 * Math.Pow(2, currentRetryCount) * (Random.Shared.NextDouble() * 0.4 + 0.8));
            return (FailureInstruction.RetrySameAccount, delay);
        }

        return retryPolicy.IsCanRetry ? (FailureInstruction.SwitchAccount, TimeSpan.Zero) : (FailureInstruction.Fail, TimeSpan.Zero);
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

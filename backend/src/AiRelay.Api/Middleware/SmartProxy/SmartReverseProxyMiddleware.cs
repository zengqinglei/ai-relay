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

    // ═══════════════════════════════════════════════════════════════════
    // InvokeAsync — 请求生命周期主编排（纯流程协调，不含业务细节）
    // ═══════════════════════════════════════════════════════════════════

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

        var downRequestHeaders = CaptureHeaders(downContext.Headers);
        var downRequestBody = downContext.IsMultipart
            ? "[Multipart Data - Logging Skipped]"
            : downContext.PreloadedBodyPreview;

        var loggingDownRequestHeaders = _loggingOptions.IsBodyLoggingEnabled ? downRequestHeaders : null;
        var loggingDownRequestBody = _loggingOptions.IsBodyLoggingEnabled ? downRequestBody : null;

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
            DownRequestHeaders: loggingDownRequestHeaders,
            DownRequestBody: loggingDownRequestBody
        ));

        try
        {
            var excludedAccountIds = new HashSet<Guid>();
            var accountSwitchCount = 0;
            const int MaxAccountSwitches = 5;

            while (true)
            {
                if (accountSwitchCount >= MaxAccountSwitches)
                    throw new ServiceUnavailableException($"已尝试 {MaxAccountSwitches} 个账号，均不可用");

                // ── 1. 选号 ──
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

                // ── 2. 指纹设置（官方账号仿真模式） ──
                await SetupFingerprintIfRequiredAsync(downContext, selectResult, context.RequestAborted);

                var currentAccountRetryCount = 1;
                var shouldSwitchAccount = false;
                var degradationLevel = 0;

                // BackoffCount=0: 3次；=1: 2次；≥2: 1次
                var maxSameAccountRetries = selectResult.BackoffCount switch
                {
                    0 => 3,
                    1 => 2,
                    _ => 1
                };

                // ── 内层循环：同账号重试 ──
                while (!shouldSwitchAccount)
                {
                    // 并发熔断感知
                    if (await smartProxyAppService.IsRateLimitedAsync(selectResult.AccountToken.Id, context.RequestAborted))
                    {
                        shouldSwitchAccount = true;
                        logger.LogDebug("账号 {AccountName} 已被并发请求触发熔断，跳过重试直接切换账号", selectResult.AccountToken.Name);
                        break;
                    }

                    var activeRequestId = Guid.CreateVersion7();

                    // ── 3. 获取并发槽位 ──
                    if (!await TryAcquireConcurrencySlotAsync(selectResult, activeRequestId, context.RequestAborted))
                    {
                        shouldSwitchAccount = true;
                        break;
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

                    var upRequestHeaders = CaptureHeaders(upContext.Headers);
                    var upRequestBody = downContext.IsMultipart
                        ? "[Multipart Data - Logging Skipped]"
                        : upContext.GetBodyPreview(downContext.PreloadedBodyPreview, _loggingOptions.MaxBodyLength);

                    var loggingUpRequestHeaders = _loggingOptions.IsBodyLoggingEnabled ? upRequestHeaders : null;
                    var loggingUpRequestBody = _loggingOptions.IsBodyLoggingEnabled ? upRequestBody : null;

                    var attemptStopwatch = Stopwatch.StartNew();
                    attemptNumber++;
                    int? httpStatusCode = null;
                    var attemptStatus = UsageStatus.Failed;
                    string? attemptStatusDesc = null;
                    string? upResponseBody = null;

                    // Step 2: 入队 AttemptStartItem
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
                        UpRequestHeaders: loggingUpRequestHeaders,
                        UpRequestBody: loggingUpRequestBody
                    ));

                    try
                    {
                        // ── Phase 1: 发送请求，等待响应头（body 未消费） ──
                        var proxyResponse = await accountedHandler.SendChatRequestAsync(
                            upContext, downContext, downContext.IsStreaming, context.RequestAborted);
                        httpStatusCode = proxyResponse.StatusCode;

                        bool isStreamCrash = false;

                        // ── Phase 2: 成功路径 — 流式转发 ──
                        if (proxyResponse.IsSuccess)
                        {
                            attemptStatus = UsageStatus.Success;
                            finalStatus = UsageStatus.Success;

                            bool isCheckStreamHealth = downContext.IsStreaming && selectResult.AccountToken.IsCheckStreamHealth;
                            var tempUpBody = new StringBuilder();
                            var tempDownBody = new StringBuilder();

                            try
                            {
                                var (crash, statusDesc, usage) = await HandleSuccessResponseAsync(
                                    context, proxyResponse, selectResult.AccountToken.Id,
                                    upContext.MappedModelId ?? downContext.ModelId,
                                    isCheckStreamHealth, tempUpBody, tempDownBody,
                                    true, // 始终允许内部 buffer 捕获，用于诊断流中途断网等异常
                                    context.RequestAborted);

                                finalUsage = usage ?? finalUsage;
                                isStreamCrash = crash;
                                attemptStatusDesc = statusDesc;

                                if (!isStreamCrash) return; // 完全成功，退出请求处理

                                // 流崩溃，回退到重试逻辑
                                attemptStatus = UsageStatus.Failed;
                                finalStatus = UsageStatus.Failed;
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                // 仅在 Response.HasStarted 时 HandleSuccessResponseAsync 会重新抛出
                                // 响应已开始下发：无法补救，标记失败后向外层传播以触发 Abort
                                attemptStatus = UsageStatus.Failed;
                                finalStatus = UsageStatus.Failed;
                                attemptStatusDesc = ex is IOException
                                    ? $"上游流传输中途断开（已向下游转发部分数据）: {ex.Message}"
                                    : $"流转发过程中发生意外异常: {ex.Message}";
                                throw;
                            }
                            finally
                            {
                                // 确保无论何种结束路径，都将本次实际传输的内容保存至日志变量
                                if (tempUpBody.Length > 0) upResponseBody = tempUpBody.ToString();
                                if (tempDownBody.Length > 0) downResponseBody = tempDownBody.ToString();
                            }
                        }

                        // ── Phase 3: 错误响应或流崩溃 — 重试决策 ──
                        if (!proxyResponse.IsSuccess || isStreamCrash)
                        {
                            if (!isStreamCrash)
                            {
                                attemptStatus = UsageStatus.Failed;
                                // 上游明确报错时，无视配置强制记录 Body
                                upResponseBody = LoggingSubBody(proxyResponse.ErrorBody, force: true);
                            }

                            logger.LogWarning("账号请求失败或流首包崩溃，状态码: {StatusCode}，正在分析错误：{BodyContent}",
                                httpStatusCode, upResponseBody);

                            var retryPolicy = isStreamCrash
                                ? new ModelErrorAnalysisResult { RetryType = RetryType.RetrySameAccount }
                                : await accountedHandler.CheckRetryPolicyAsync(
                                    httpStatusCode!.Value, downContext.RelativePath, proxyResponse.Headers, proxyResponse.ErrorBody);

                            if (!string.IsNullOrEmpty(retryPolicy.Description))
                            {
                                attemptStatusDesc = retryPolicy.Description;
                                logger.LogDebug("错误分析结果: {Description}", retryPolicy.Description);
                            }

                            var (instruction, retryAfter) = DetermineFailureInstruction(
                                retryPolicy, currentAccountRetryCount, maxSameAccountRetries, accountSwitchCount);

                            switch (instruction)
                            {
                                case FailureInstruction.RetrySameAccount:
                                    if (retryPolicy.RetryType == RetryType.RetrySameAccountWithDowngrade)
                                    {
                                        degradationLevel++;
                                        attemptStatusDesc = $"启用降级级别 {degradationLevel} 进行重试 (状态码: {httpStatusCode}, Retry {currentAccountRetryCount})" +
                                            (attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "");
                                    }
                                    else
                                    {
                                        attemptStatusDesc = $"同账号重试 (状态码: {httpStatusCode})，延迟 {retryAfter.TotalMilliseconds}ms，重试次数: {currentAccountRetryCount}" +
                                            (attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "");
                                    }
                                    await Task.Delay(retryAfter, context.RequestAborted);
                                    currentAccountRetryCount++;
                                    break;

                                case FailureInstruction.SwitchAccount:
                                    await smartProxyAppService.HandleFailureAsync(
                                        new HandleFailureInputDto(
                                            selectResult.AccountToken.Id,
                                            httpStatusCode!.Value,
                                            proxyResponse.ErrorBody,
                                            downContext.ModelId,
                                            upContext.MappedModelId ?? downContext.ModelId,
                                            retryPolicy),
                                        context.RequestAborted);
                                    shouldSwitchAccount = true;
                                    attemptStatusDesc = $"账号 '{selectResult.AccountToken.Name}' 不可用 (状态码: {httpStatusCode})，尝试切换至其他资源进行重试" +
                                        (attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "");
                                    break;

                                case FailureInstruction.Fail:
                                    if (retryPolicy.RetryType != RetryType.UnsupportedEndpoint)
                                    {
                                        await smartProxyAppService.HandleFailureAsync(
                                            new HandleFailureInputDto(
                                                selectResult.AccountToken.Id,
                                                httpStatusCode!.Value,
                                                proxyResponse.ErrorBody,
                                                downContext.ModelId,
                                                upContext.MappedModelId ?? downContext.ModelId,
                                                retryPolicy),
                                            context.RequestAborted);
                                    }
                                    attemptStatusDesc = retryPolicy.RetryType == RetryType.UnsupportedEndpoint
                                        ? $"端点不支持 (状态码: {httpStatusCode})，直接透传响应：{retryPolicy.Description}"
                                        : $"请求最终失败 (状态码: {httpStatusCode})，不进行重试：{upResponseBody}";
                                    WriteResponseHeaders(context, httpStatusCode!.Value, proxyResponse.Headers);
                                    downResponseBody = LoggingSubBody(proxyResponse.ErrorBody, force: true);
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
                            UpResponseBody: upResponseBody,
                            UpRequestHeaders: attemptStatus == UsageStatus.Failed ? upRequestHeaders : loggingUpRequestHeaders,
                            UpRequestBody: attemptStatus == UsageStatus.Failed ? upRequestBody : loggingUpRequestBody
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
            downResponseBody = LoggingSubBody(errorResponse.Payload, force: true);

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
                DownStatusCode: context.Response.StatusCode,
                DownRequestHeaders: finalStatus == UsageStatus.Failed ? downRequestHeaders : loggingDownRequestHeaders,
                DownRequestBody: finalStatus == UsageStatus.Failed ? downRequestBody : loggingDownRequestBody
            ));
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 指纹设置（官方账号仿真模式）
    // ═══════════════════════════════════════════════════════════════════

    private async Task SetupFingerprintIfRequiredAsync(
        DownRequestContext downContext,
        SelectAccountResultDto selectResult,
        CancellationToken ct)
    {
        if (!selectResult.AccountToken.AllowOfficialClientMimic) return;

        downContext.StickySessionId = await fingerprintAppService.GenerateSessionUuidAsync(
            selectResult.AccountToken.Id,
            downContext.SessionId,
            selectResult.AccountToken.ExtraProperties.TryGetValue("session_id_masking_enabled", out var maskingValue)
                && bool.TryParse(maskingValue, out var enabled) && enabled,
            ct);

        var fingerprint = await fingerprintAppService.GetOrCreateFingerprintAsync(
            selectResult.AccountToken.Id,
            downContext.Headers,
            ct);
        downContext.FingerprintClientId = fingerprint.ClientId;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 并发槽位获取
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 尝试获取并发槽位。
    /// 返回 <c>true</c> 表示槽位获取成功（含等待后成功）；
    /// 返回 <c>false</c> 表示无槽且配置为不等待，调用方应切换账号。
    /// </summary>
    private async Task<bool> TryAcquireConcurrencySlotAsync(
        SelectAccountResultDto selectResult,
        Guid activeRequestId,
        CancellationToken ct)
    {
        bool acquired = await concurrencyStrategy.AcquireSlotAsync(
            selectResult.AccountToken.Id, activeRequestId, selectResult.WaitPlan.MaxConcurrency, ct);

        if (acquired) return true;
        if (!selectResult.WaitPlan.ShouldWait) return false; // 无槽且不等待，交由调用方切号

        // 进入等待队列
        var maxWait = selectResult.WaitPlan.MaxConcurrency + 20;
        if (!await concurrencyStrategy.IncrementWaitCountAsync(selectResult.AccountToken.Id, maxWait, ct))
            throw new ServiceUnavailableException("等待队列已满，请稍后重试");

        try
        {
            acquired = await concurrencyStrategy.WaitForSlotAsync(
                selectResult.AccountToken.Id,
                activeRequestId,
                selectResult.WaitPlan.MaxConcurrency,
                selectResult.WaitPlan.Timeout,
                ct);
        }
        finally
        {
            await concurrencyStrategy.DecrementWaitCountAsync(selectResult.AccountToken.Id, ct);
        }

        if (!acquired)
            throw new ServiceUnavailableException($"账号 {selectResult.AccountToken.Name} 繁忙，请稍后重试");

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 成功路径：SSE 事件流转发（含首包健康检查）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 枚举并转发上游 SSE 事件流，处理首包健康检查与缓冲逻辑。
    /// <para>
    /// 返回 <c>(IsStreamCrash, StatusDesc, Usage)</c>：
    /// IsStreamCrash=false 表示完全成功（调用方应 return），true 表示流崩溃需重试。
    /// </para>
    /// <para>
    /// 若 <c>Response.HasStarted</c> 时发生异常，方法会直接重新抛出，
    /// 由调用方设置状态描述并继续向上传播以触发连接 Abort。
    /// </para>
    /// </summary>
    private async Task<(bool IsStreamCrash, string? StatusDesc, ResponseUsage? Usage)> HandleSuccessResponseAsync(
        HttpContext context,
        ProxyResponse proxyResponse,
        Guid accountTokenId,
        string? upModelId,
        bool isCheckStreamHealth,
        StringBuilder tempUpBody,
        StringBuilder tempDownBody,
        bool forceBodyCapture,
        CancellationToken ct)
    {
        bool headersWritten = false;
        bool isStreamCrash = false;
        string? statusDesc = null;
        ResponseUsage? usage = null;
        var bufferedBytes = new List<byte[]>();

        if (!isCheckStreamHealth)
        {
            // 未开启健康检查，立即放行，发送响应头
            await smartProxyAppService.HandleSuccessAsync(accountTokenId, upModelId, ct);
            WriteResponseHeaders(context, proxyResponse.StatusCode, proxyResponse.Headers);
            headersWritten = true;
        }

        try
        {
            await foreach (var evt in proxyResponse.Events!.WithCancellation(ct))
            {
                // 收集 Token 用量
                if (evt.IsComplete && evt.Usage != null)
                    usage = evt.Usage;

                // 健康检查门控（流式 + 尚未写响应头时）
                if (isCheckStreamHealth && !headersWritten)
                {
                    if (evt.Type == StreamEventType.Error)
                    {
                        isStreamCrash = true;
                        statusDesc = $"流健康检查到内部错误事件节点 '{evt.Content ?? "unknown"}'";
                        break;
                    }

                    if (evt.HasOutput)
                    {
                        // 实际输出产生，解除缓冲，放行响应
                        await smartProxyAppService.HandleSuccessAsync(accountTokenId, upModelId, ct);
                        WriteResponseHeaders(context, proxyResponse.StatusCode, proxyResponse.Headers);
                        headersWritten = true;

                        // 将健康检查期间缓冲的数据一次性冲刷到下游
                        await FlushBufferedBytesAsync(context, bufferedBytes, tempDownBody, forceBodyCapture, ct);
                    }
                }

                // 转发字节
                var bytesToForward = evt.ConvertedBytes ?? evt.OriginalBytes;
                if (bytesToForward == null) continue;

                // 上游原始数据日志（不受健康检查状态影响）
                AppendBodyLog(tempUpBody, evt.OriginalBytes, forceBodyCapture);

                if (isCheckStreamHealth && !headersWritten)
                {
                    // 健康检查期间缓存，等待有效内容确认后再冲刷
                    bufferedBytes.Add(bytesToForward);
                }
                else
                {
                    // 直接写入下游
                    AppendBodyLog(tempDownBody, bytesToForward, forceBodyCapture);
                    await context.Response.Body.WriteAsync(bytesToForward, ct);
                    await context.Response.Body.FlushAsync(ct);
                }
            }

            // 流枚举结束，健康检查仍未放行 → 判定为空流
            if (isCheckStreamHealth && !headersWritten && !isStreamCrash)
            {
                isStreamCrash = true;
                statusDesc = "流健康检查未读取到包含有效文本，判定为空流或无响应";
            }

            if (isStreamCrash)
            {
                context.Response.Clear();
                logger.LogWarning(statusDesc);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (context.Response.HasStarted)
            {
                // 响应已发出部分数据，无法回滚，向上抛出由外层 Abort 处理
                throw;
            }

            // 响应未开始，标记流崩溃并回滚，触发同号或切号重试
            context.Response.Clear();
            isStreamCrash = true;
            statusDesc = ex is IOException
                ? $"上游流在首包前断开，触发同号重试或切号重试机制: {ex.Message}"
                : $"流尚未开始下发便中断异常，触发同号重试或切号重试机制: {ex.Message}";
            logger.LogWarning(ex, statusDesc);
        }

        return (isStreamCrash, statusDesc, usage);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 将缓冲块写入下游并同步记录日志
    // ═══════════════════════════════════════════════════════════════════

    private async Task FlushBufferedBytesAsync(
        HttpContext context,
        List<byte[]> bufferedBytes,
        StringBuilder tempDownBody,
        bool forceBodyCapture,
        CancellationToken ct)
    {
        foreach (var chunk in bufferedBytes)
        {
            AppendBodyLog(tempDownBody, chunk, forceBodyCapture);
            await context.Response.Body.WriteAsync(chunk, ct);
        }

        bufferedBytes.Clear();
        await context.Response.Body.FlushAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Body 日志追加（含截断保护）
    // ═══════════════════════════════════════════════════════════════════

    private void AppendBodyLog(StringBuilder target, byte[]? data, bool force = false)
    {
        if (data == null) return;
        if (!force && !_loggingOptions.IsBodyLoggingEnabled) return;
        if (target.Length >= _loggingOptions.MaxBodyLength) return;
        var maxLength = _loggingOptions.MaxBodyLength - target.Length;
        var length = Math.Min(data.Length, maxLength);
        target.Append(Encoding.UTF8.GetString(data, 0, length));
        if (data.Length > maxLength) target.Append("...[Truncated]");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 通用辅助方法
    // ═══════════════════════════════════════════════════════════════════

    private string? LoggingSubBody(string? sourceStr, bool force = false)
    {
        if (string.IsNullOrEmpty(sourceStr)) return sourceStr;
        if (!force && !_loggingOptions.IsBodyLoggingEnabled) return null;
        var length = Math.Min(sourceStr.Length, _loggingOptions.MaxBodyLength);
        var content = sourceStr[..length];
        return sourceStr.Length > _loggingOptions.MaxBodyLength ? content + "...[Truncated]" : content;
    }

    private async Task<DownRequestContext> ProcessDownstreamRequestAsync(
        HttpContext context, RouteProfile routeProfile, IChatModelHandler chatModelHandler, Guid apiKeyId)
    {
        var request = context.Request;
        var contentType = request.ContentType ?? "";
        var isMultipart = contentType.Contains("multipart", StringComparison.OrdinalIgnoreCase);

        const long MaxBodySize = 100 * 1024 * 1024; // 100MB
        bool hasBody = request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding");
        if (hasBody && !isMultipart)
        {
            if (request.ContentLength > MaxBodySize)
                throw new BadRequestException($"Request body too large, limit is {MaxBodySize / (1024 * 1024)}MB");

            request.EnableBuffering(MaxBodySize);
        }

        var pathPrefix = RouteProfileRegistry.Profiles.TryGetValue(routeProfile, out var profileDef)
            ? profileDef.PathPrefix
            : string.Empty;

        var relativePath = pathPrefix;
        if (context.Request.RouteValues.TryGetValue("catch-all", out var catchAll) && catchAll != null)
        {
            var catchAllPath = catchAll.ToString()!;
            var separator = pathPrefix.Contains(':') ? ":" : "/";
            relativePath = string.IsNullOrEmpty(catchAllPath)
                ? pathPrefix
                : $"{pathPrefix}{separator}{catchAllPath}";
        }

        var rawStream = (hasBody && !isMultipart) ? request.Body : null;
        var (extractedProps, bodyPreview) = await JsonExtractHelper.ExtractEssentialPropsAsync(
            rawStream, _loggingOptions.IsBodyLoggingEnabled, _loggingOptions.MaxBodyLength);

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
        "connection" or "keep-alive" or "transfer-encoding" or "te" or "trailer" or
        "proxy-authorization" or "proxy-authenticate" or "upgrade" or "expect" or "proxy-connection" => true,
        _ => false
    };

    private static void WriteResponseHeaders(
        HttpContext context, int statusCode, Dictionary<string, IEnumerable<string>> headers)
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
        int maxRetries,
        int accountSwitchCount)
    {
        // 0. 端点不支持：直接透传，不重试不切号不计熔断
        if (retryPolicy.RetryType == RetryType.UnsupportedEndpoint)
            return (FailureInstruction.Fail, TimeSpan.Zero);

        var canRetry = retryPolicy.RetryType is RetryType.RetrySameAccount
                                              or RetryType.RetrySameAccountWithDowngrade;

        // 1. 同账号重试判定
        if (canRetry && currentRetryCount < maxRetries)
        {
            // 如果上游明确要求等待超过 15s，通常意味着该账号被严重限流，直接切号
            if (retryPolicy.RetryAfter.HasValue && retryPolicy.RetryAfter.Value.TotalSeconds >= 15)
                return (FailureInstruction.SwitchAccount, TimeSpan.Zero);

            var delay = retryPolicy.RetryAfter ?? TimeSpan.FromMilliseconds(
                1000 * Math.Pow(2, currentRetryCount) * (Random.Shared.NextDouble() * 0.4 + 0.8));
            return (FailureInstruction.RetrySameAccount, delay);
        }

        // 2. 切换账号判定
        // 情况 A: Handler 判定可重试，但同账号次数已满，则切换到下一个号继续试
        if (canRetry)
            return (FailureInstruction.SwitchAccount, TimeSpan.Zero);

        // 情况 B: Handler 判定不可重试（如官方 5xx 或 401/403 等）
        // 第一个号报错时额外给一次切号机会（盲切补偿）
        if (accountSwitchCount == 0)
            return (FailureInstruction.SwitchAccount, TimeSpan.Zero);

        // 其他情况（已切过号但还是报错）则直接失败
        return (FailureInstruction.Fail, TimeSpan.Zero);
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



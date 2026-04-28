using System.Diagnostics;
using System.Text;
using AiRelay.Application.ModelRoutes.Dtos;
using AiRelay.Application.ModelRoutes.Handlers;
using AiRelay.Application.ProviderAccounts.AppServices;
using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Application.UsageRecords.Queue;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.Options;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Domain.UsageRecords.ValueObjects;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.ModelRoutes;

public class ModelRouteAppService(
    AccountTokenDomainService accountTokenDomainService,
    AccountResultHandlerDomainService accountResultHandlerDomainService,
    AccountRateLimitDomainService rateLimitDomainService,
    AccountFingerprintAppService fingerprintAppService,
    AccountRetryStrategyDomainService accountRetryStrategyDomainService,
    RouteAccountSchedulingDomainService routeAccountSchedulingDomainService,
    IProviderGroupRepository providerGroupRepository,
    IProviderGroupAccountRelationRepository relationRepository,
    IRepository<ApiKeyProviderGroupBinding, Guid> apiKeyProviderGroupBindingRepository,
    IRepository<AccountToken, Guid> accountRepository,
    IObjectMapper objectMapper,
    IConcurrencyStrategy concurrencyStrategy,
    IQueryableAsyncExecuter queryableAsyncExecuter,
    IChatModelHandlerFactory chatModelHandlerFactory,
    IUsageRecordQueue usageRecordQueue,
    IOptions<UsageLoggingOptions> loggingOptions,
    IOptions<ModelSchedulingOptions> schedulingOptions,
    ILogger<ModelRouteAppService> logger) : BaseAppService, IModelRouteAppService
{
    private readonly UsageLoggingOptions _loggingOptions = loggingOptions.Value;
    private readonly ModelSchedulingOptions _schedulingOptions = schedulingOptions.Value;

    public async Task<IReadOnlyList<RouteAccountSchedulingGroup>> ResolveProxyRouteCandidatesAsync(
        SelectProxyAccountInputDto input,
        CancellationToken cancellationToken = default)
    {
        var bindingGroupQuery = await apiKeyProviderGroupBindingRepository
            .GetQueryIncludingAsync(cancellationToken, p => p.ProviderGroup);

        var bindings = await queryableAsyncExecuter.ToListAsync(
            bindingGroupQuery
                .Where(b => b.ApiKeyId == input.ApiKeyId)
                .OrderBy(b => b.Priority));

        if (!bindings.Any())
        {
            throw new ForbiddenException($"ApiKey '{input.ApiKeyName}' 未绑定任何资源池，无法选择账户");
        }

        var candidateGroups = await BuildSchedulingGroupsAsync(
            bindings.Select((binding, index) => (binding.ProviderGroup, index)).ToList(),
            input.AllowedCombinations,
            cancellationToken);

        if (candidateGroups.Count == 0)
        {
            throw new ServiceUnavailableException($"所有绑定的资源池中均没有符合协议的活跃账号以支撑请求 (所需模型: {input.ModelId})");
        }

        return candidateGroups;
    }

    public async Task<IReadOnlyList<RouteAccountSchedulingGroup>> ResolveWorkspaceRouteCandidatesAsync(
        SelectWorkspaceAccountInputDto input,
        CancellationToken cancellationToken = default)
    {
        if (!input.ProviderGroupId.HasValue)
        {
            if (input.AccountId.HasValue)
            {
                throw new BadRequestException("固定账户模式下必须显式选择资源池分组");
            }

            var visibleGroups = await providerGroupRepository.GetVisibleGroupsAsync(input.UserId, cancellationToken);
            var candidateGroups = await BuildSchedulingGroupsAsync(
                visibleGroups.Select((group, index) => (group, index)).ToList(),
                allowedCombinations: null,
                cancellationToken);

            if (candidateGroups.Count == 0)
            {
                throw new ServiceUnavailableException($"当前可见资源池中没有活跃账号支持模型 {input.ModelId}");
            }

            return candidateGroups;
        }

        var targetGroup = await providerGroupRepository.GetVisibleByIdAsync(input.ProviderGroupId.Value, input.UserId, cancellationToken)
            ?? throw new NotFoundException($"资源池不存在: {input.ProviderGroupId}");

        if (input.AccountId.HasValue)
        {
            var candidates = await relationRepository.GetCandidatesAsync(input.ProviderGroupId.Value, cancellationToken: cancellationToken);
            var relation = candidates.FirstOrDefault(x => x.AccountTokenId == input.AccountId.Value);

            if (relation?.AccountToken == null)
            {
                throw new NotFoundException($"固定账户不存在或未绑定到资源池: {input.AccountId}");
            }

            return [new RouteAccountSchedulingGroup(targetGroup, [relation], 0)];
        }

        var singleGroupCandidates = await BuildSchedulingGroupsAsync([(targetGroup, 0)], allowedCombinations: null, cancellationToken);
        if (singleGroupCandidates.Count == 0)
        {
            throw new ServiceUnavailableException($"资源池 '{targetGroup.Name}' 中没有可用账号支持模型 {input.ModelId}");
        }

        return singleGroupCandidates;
    }

    private async Task<SelectAccountResultDto> PrepareSelectedAccountAsync(
        RouteAccountSchedulingResult selectedAccount,
        string sessionHash,
        Dictionary<string, string> downHeaders,
        CancellationToken cancellationToken)
    {
        var accountToken = selectedAccount.AccountToken;
        var providerGroup = selectedAccount.ProviderGroup;
        var isStickyBound = selectedAccount.IsStickyBound;
        var availableCount = selectedAccount.AvailableCount;

        await accountTokenDomainService.RefreshTokenIfNeededAsync(accountToken, cancellationToken);

        if (string.IsNullOrEmpty(accountToken.AccessToken) && accountToken.AuthMethod != AuthMethod.ApiKey) // 仅非APIKey检查AccessToken
        {
            throw new UnauthorizedException($"{accountToken.Provider} 账户 '{accountToken.Name}' 的凭证为空");
        }

        logger.LogDebug("已选定账户: {AccountTokenName} (ProviderGroupId: {ProviderGroupId}, IsStickyBound: {IsStickyBound})",
            accountToken.Name, providerGroup.Id, isStickyBound);

        // 决定等待策略
        var waitPlan = new WaitPlan
        {
            ShouldWait = isStickyBound,
            Timeout = isStickyBound
                ? TimeSpan.FromSeconds(_schedulingOptions.StickyWaitTimeoutSeconds)
                : TimeSpan.FromSeconds(_schedulingOptions.NonStickyWaitTimeoutSeconds),
            MaxConcurrency = accountToken.MaxConcurrency,
            IsStickyBound = isStickyBound
        };

        // 预取并发数据
        var concurrencyCount = await concurrencyStrategy.GetConcurrencyCountAsync(accountToken.Id, cancellationToken);
        var contextItems = new Dictionary<string, object>
        {
            ["ConcurrencyCounts"] = new Dictionary<Guid, int> { [accountToken.Id] = concurrencyCount }
        };

        var accountTokenResult = objectMapper.Map<AccountToken, AvailableAccountTokenOutputDto>(accountToken, contextItems);
        var backoffCount = await rateLimitDomainService.GetBackoffCountAsync(accountToken.Id, cancellationToken);

        // 4. 初始化官方指纹（若需要）
        FingerprintSetupResult? fingerprint = null;
        if (accountTokenResult.AllowOfficialClientMimic)
        {
            var enableMasking = accountTokenResult.ExtraProperties.TryGetValue("session_id_masking_enabled", out var maskingValue)
                && bool.TryParse(maskingValue, out var enabled) && enabled;

            var stickySessionId = await fingerprintAppService.GenerateSessionUuidAsync(
                accountToken.Id, sessionHash, enableMasking, cancellationToken);

            var fingerprintResult = await fingerprintAppService.GetOrCreateFingerprintAsync(
                accountToken.Id, downHeaders, cancellationToken);

            fingerprint = new FingerprintSetupResult(stickySessionId, fingerprintResult.ClientId);
        }

        return new SelectAccountResultDto
        {
            AccountToken = accountTokenResult,
            ProviderGroupId = providerGroup.Id,
            ProviderGroupName = providerGroup.Name,
            GroupRateMultiplier = providerGroup.RateMultiplier,
            WaitPlan = waitPlan,
            AvailableAccountCount = availableCount,
            MaxSameAccountRetries = backoffCount switch { 0 => 3, 1 => 2, _ => 1 },
            Fingerprint = fingerprint
        };
    }

    public async Task ExecuteRouteAsync(
        DownRequestContext baseDownContext,
        RouteExecutionMetadata metadata,
        IReadOnlyList<RouteAccountSchedulingGroup> candidateGroups,
        Func<SelectAccountResultDto, DownRequestContext> downContextModifier,
        IRouteResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var attemptNumber = 0;
        var overallStopwatch = Stopwatch.StartNew();
        var finalStatus = UsageStatus.Failed;
        string? finalStatusDescription = null;
        string? downResponseBody = null;
        ResponseUsage? finalUsage = null;
        int? finalDownStatusCode = null;

        var downRequestHeaders = CaptureHeaders(baseDownContext.Headers);
        var downRequestBody = baseDownContext.IsMultipart ? "[Multipart Data - Logging Skipped]" : baseDownContext.PreloadedBodyPreview;

        var loggingDownRequestHeaders = _loggingOptions.IsBodyLoggingEnabled ? downRequestHeaders : null;
        var loggingDownRequestBody = _loggingOptions.IsBodyLoggingEnabled ? downRequestBody : null;

        usageRecordQueue.TryEnqueue(new UsageRecordStartItem(
            UsageRecordId: metadata.UsageRecordId,
            UserId: metadata.UserId,
            Source: metadata.Source,
            CorrelationId: metadata.CorrelationId,
            SessionId: baseDownContext.SessionId,
            ApiKeyId: metadata.ApiKeyId,
            ApiKeyName: metadata.ApiKeyName,
            IsStreaming: baseDownContext.IsStreaming,
            DownRequestMethod: baseDownContext.Method.Method,
            DownRequestUrl: baseDownContext.DownRequestUrl ?? baseDownContext.RelativePath,
            DownModelId: baseDownContext.ModelId,
            DownClientIp: baseDownContext.ClientIp,
            DownUserAgent: baseDownContext.GetUserAgent(),
            DownRequestHeaders: loggingDownRequestHeaders,
            DownRequestBody: loggingDownRequestBody
        ));

        try
        {
            var excludedAccountIds = new HashSet<Guid>();
            var maxCandidateAttempts = Math.Min(
                candidateGroups.SelectMany(group => group.CandidateRelations)
                    .Select(relation => relation.AccountTokenId)
                    .Distinct()
                    .Count(),
                _schedulingOptions.MaxAccountSwitches);

            while (true)
            {
                var selectedAccount = await SelectRouteAccountAsync(
                    candidateGroups,
                    baseDownContext.SessionId ?? string.Empty,
                    baseDownContext.ModelId,
                    baseDownContext.Headers,
                    excludedAccountIds,
                    cancellationToken);

                SelectAccountResultDto selectResult;
                try
                {
                    selectResult = await PrepareSelectedAccountAsync(
                        selectedAccount,
                        baseDownContext.SessionId ?? string.Empty,
                        baseDownContext.Headers,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var prepareFailureStatusCode = ResolveInternalExceptionStatusCode(ex);
                    var prepareFailureDesc = $"账号 '{selectedAccount.AccountToken.Name}' 在发起上游请求前准备失败: {ex.Message}";
                    var prepareFailureBody = LoggingSubBody(ex.Message);

                    attemptNumber++;
                    EnqueueAttemptStart(
                        metadata.UsageRecordId,
                        attemptNumber,
                        selectedAccount.AccountToken,
                        selectedAccount.ProviderGroup,
                        null,
                        null,
                        null,
                        null,
                        null);
                    EnqueueAttemptEnd(
                        metadata.UsageRecordId,
                        attemptNumber,
                        prepareFailureStatusCode,
                        0,
                        UsageStatus.Failed,
                        prepareFailureDesc,
                        prepareFailureBody,
                        null,
                        null);

                    logger.LogWarning(ex, prepareFailureDesc);

                    await HandleFailureAsync(new HandleFailureInputDto(
                        selectedAccount.AccountToken.Id,
                        prepareFailureStatusCode,
                        ex.Message,
                        baseDownContext.ModelId,
                        baseDownContext.ModelId,
                        new ModelErrorAnalysisResult
                        {
                            RetryType = RetryType.NoRetry,
                            Description = prepareFailureDesc
                        }), cancellationToken);

                    excludedAccountIds.Add(selectedAccount.AccountToken.Id);
                    if (excludedAccountIds.Count >= maxCandidateAttempts)
                    {
                        throw;
                    }

                    continue;
                }

                var currentAccountRetryCount = 1;
                var shouldSwitchAccount = false;
                var degradationLevel = 0;
                var maxSameAccountRetries = selectResult.MaxSameAccountRetries;

                while (!shouldSwitchAccount)
                {
                    var downContext = downContextModifier(selectResult);

                    if (selectResult.Fingerprint != null)
                    {
                        downContext.StickySessionId = selectResult.Fingerprint.StickySessionId;
                        downContext.FingerprintClientId = selectResult.Fingerprint.FingerprintClientId;
                    }

                    var activeRequestId = Guid.CreateVersion7();

                    await using var slot = await TryAcquireReadySlotAsync(selectResult, activeRequestId, cancellationToken);
                    if (!slot.Acquired)
                    {
                        attemptNumber++;
                        var schedulingFailureDesc = slot.FailureDescription
                            ?? $"账号 '{selectResult.AccountToken.Name}' 在发起上游请求前因调度失败被切换";
                        EnqueueAttemptStart(
                            metadata.UsageRecordId,
                            attemptNumber,
                            selectResult,
                            null,
                            null,
                            null,
                            null,
                            null);
                        EnqueueAttemptEnd(
                            metadata.UsageRecordId,
                            attemptNumber,
                            null,
                            0,
                            UsageStatus.Failed,
                            schedulingFailureDesc,
                            null,
                            null,
                            null);
                        logger.LogWarning(schedulingFailureDesc);
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

                    var upContext = await accountedHandler.ProcessRequestContextAsync(downContext, degradationLevel, cancellationToken);

                    var upRequestHeaders = CaptureHeaders(upContext.Headers);
                    var upRequestBody = downContext.IsMultipart ? "[Multipart Data - Logging Skipped]" : upContext.GetBodyPreview(downContext.PreloadedBodyPreview, _loggingOptions.MaxBodyLength);

                    var loggingUpRequestHeaders = _loggingOptions.IsBodyLoggingEnabled ? upRequestHeaders : null;
                    var loggingUpRequestBody = _loggingOptions.IsBodyLoggingEnabled ? upRequestBody : null;

                    var attemptStopwatch = Stopwatch.StartNew();
                    attemptNumber++;
                    int? httpStatusCode = null;
                    var attemptStatus = UsageStatus.Failed;
                    string? attemptStatusDesc = null;
                    string? upResponseBody = null;
                    ModelErrorAnalysisResult? streamFailureAnalysis = null;

                    EnqueueAttemptStart(
                        metadata.UsageRecordId,
                        attemptNumber,
                        selectResult,
                        upContext.MappedModelId,
                        upContext.GetUserAgent(),
                        upContext.GetFullUrl(),
                        loggingUpRequestHeaders,
                        loggingUpRequestBody);

                    try
                    {
                        var proxyResponse = await accountedHandler.SendChatRequestAsync(upContext, downContext, downContext.IsStreaming, cancellationToken);
                        httpStatusCode = proxyResponse.StatusCode;

                        bool isStreamCrash = false;

                        if (proxyResponse.IsSuccess)
                        {
                            attemptStatus = UsageStatus.Success;
                            finalStatus = UsageStatus.Success;

                            bool isCheckStreamHealth = downContext.IsStreaming && selectResult.AccountToken.IsCheckStreamHealth;
                            var tempUpBody = new StringBuilder();
                            var tempDownBody = new StringBuilder();

                            try
                            {
                                var (crash, statusDesc, usage, failureAnalysis) = await HandleSuccessResponseAsync(
                                    responseHandler, proxyResponse, selectResult.AccountToken.Id,
                                    upContext.MappedModelId ?? downContext.ModelId,
                                    isCheckStreamHealth, tempUpBody, tempDownBody, _loggingOptions.IsBodyLoggingEnabled, cancellationToken);

                                finalUsage = usage ?? finalUsage;
                                isStreamCrash = crash;
                                streamFailureAnalysis = failureAnalysis;
                                attemptStatusDesc = streamFailureAnalysis?.Description ?? statusDesc;

                                if (!isStreamCrash)
                                {
                                    finalDownStatusCode = proxyResponse.StatusCode;
                                    return;
                                }

                                attemptStatus = UsageStatus.Failed;
                                finalStatus = UsageStatus.Failed;
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                attemptStatus = UsageStatus.Failed;
                                finalStatus = UsageStatus.Failed;
                                attemptStatusDesc = ex is IOException
                                    ? $"上游流传输中途断开（已向下游转发部分数据）: {ex.Message}"
                                    : $"流转发过程中发生意外异常: {ex.Message}";
                                throw;
                            }
                            finally
                            {
                                if (tempUpBody.Length > 0) upResponseBody = tempUpBody.ToString();
                                if (tempDownBody.Length > 0) downResponseBody = tempDownBody.ToString();
                            }
                        }

                        if (!proxyResponse.IsSuccess || isStreamCrash)
                        {
                            if (!isStreamCrash)
                            {
                                attemptStatus = UsageStatus.Failed;
                                upResponseBody = LoggingSubBody(proxyResponse.ErrorBody);
                            }

                            logger.LogWarning("账号请求失败或流首包崩溃，状态码: {StatusCode}，正在分析错误：{BodyContent}", httpStatusCode, upResponseBody);

                            var retryPolicy = isStreamCrash && streamFailureAnalysis != null
                                ? streamFailureAnalysis
                                : await accountedHandler.CheckRetryPolicyAsync(httpStatusCode!.Value, downContext.RelativePath, proxyResponse.Headers, proxyResponse.ErrorBody);

                            if (!string.IsNullOrEmpty(retryPolicy.Description))
                            {
                                attemptStatusDesc = retryPolicy.Description;
                                logger.LogDebug("错误分析结果: {Description}", retryPolicy.Description);
                            }

                            var (instruction, retryAfter) = DetermineFailureInstruction(retryPolicy, currentAccountRetryCount, maxSameAccountRetries, excludedAccountIds.Count);

                            switch (instruction)
                            {
                                case FailureInstruction.RetrySameAccount:
                                    if (retryPolicy.RetryType == RetryType.RetrySameAccountWithDowngrade)
                                    {
                                        degradationLevel++;
                                        attemptStatusDesc = $"启用降级级别 {degradationLevel} 进行重试 (状态码: {httpStatusCode}, Retry {currentAccountRetryCount})" + (attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "");
                                    }
                                    else
                                    {
                                        attemptStatusDesc = $"同账号重试 (状态码: {httpStatusCode})，延迟 {retryAfter.TotalMilliseconds}ms，重试次数: {currentAccountRetryCount}" + (attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "");
                                    }
                                    await Task.Delay(retryAfter, cancellationToken);
                                    currentAccountRetryCount++;
                                    break;

                                case FailureInstruction.SwitchAccount:
                                    await HandleFailureAsync(new HandleFailureInputDto(selectResult.AccountToken.Id, httpStatusCode!.Value, proxyResponse.ErrorBody, downContext.ModelId, upContext.MappedModelId ?? downContext.ModelId, retryPolicy), cancellationToken);
                                    shouldSwitchAccount = true;
                                    attemptStatusDesc = $"账号 '{selectResult.AccountToken.Name}' 不可用 (状态码: {httpStatusCode})，尝试切换至其他资源进行重试" + (attemptStatusDesc != null ? $"：{attemptStatusDesc}" : "");
                                    break;

                                case FailureInstruction.Fail:
                                    if (retryPolicy.RetryType != RetryType.UnsupportedEndpoint)
                                    {
                                        await HandleFailureAsync(new HandleFailureInputDto(selectResult.AccountToken.Id, httpStatusCode!.Value, proxyResponse.ErrorBody, downContext.ModelId, upContext.MappedModelId ?? downContext.ModelId, retryPolicy), cancellationToken);
                                    }
                                    attemptStatusDesc = retryPolicy.RetryType == RetryType.UnsupportedEndpoint
                                        ? $"端点不支持 (状态码: {httpStatusCode})，直接透传响应：{retryPolicy.Description}"
                                        : $"请求最终失败 (状态码: {httpStatusCode})，不进行重试：{upResponseBody}";
                                    finalStatusDescription = attemptStatusDesc;
                                    finalDownStatusCode = httpStatusCode;
                                    downResponseBody = LoggingSubBody(await responseHandler.OnTerminalErrorAsync(
                                        RouteTerminalError.UpstreamNormalized(httpStatusCode ?? 500, proxyResponse.ErrorBody),
                                        cancellationToken));
                                    return;
                            }

                            if (!string.IsNullOrEmpty(attemptStatusDesc)) logger.LogWarning(attemptStatusDesc);
                        }
                    }
                    finally
                    {
                        attemptStopwatch.Stop();
                        EnqueueAttemptEnd(
                            metadata.UsageRecordId,
                            attemptNumber,
                            httpStatusCode,
                            attemptStopwatch.ElapsedMilliseconds,
                            attemptStatus,
                            attemptStatusDesc,
                            upResponseBody,
                            attemptStatus == UsageStatus.Failed ? upRequestHeaders : loggingUpRequestHeaders,
                            attemptStatus == UsageStatus.Failed ? upRequestBody : loggingUpRequestBody);
                    }
                }

                if (shouldSwitchAccount)
                {
                    excludedAccountIds.Add(selectResult.AccountToken.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            finalStatus = UsageStatus.Failed;
            finalStatusDescription = "客户端主动断开连接";
            finalDownStatusCode ??= 499;
            logger.LogInformation("原始客户端已强制断开连接，提前终止代理。");
            responseHandler.AbortConnection();
        }
        catch (Exception ex)
        {
            finalStatus = UsageStatus.Failed;
            if (responseHandler.HasResponseStarted)
            {
                finalStatusDescription = ex is IOException ? $"上游流传输中途断开: {ex.Message}" : $"代理转发过程中发生意外异常: {ex.Message}";
                finalDownStatusCode ??= 200;
                logger.LogWarning(ex, finalStatusDescription);
                responseHandler.AbortConnection();
                return;
            }
            else
            {
                finalStatusDescription = $"代理网关异常被拦截: {ex.Message}";
                finalDownStatusCode = ResolveInternalExceptionStatusCode(ex);
                logger.LogWarning(ex, finalStatusDescription);
            }

            downResponseBody = LoggingSubBody(await responseHandler.OnTerminalErrorAsync(
                RouteTerminalError.InternalException(ex, finalDownStatusCode ?? 500, finalStatusDescription),
                cancellationToken));
        }
        finally
        {
            overallStopwatch.Stop();
            usageRecordQueue.TryEnqueue(new UsageRecordEndItem(
                UsageRecordId: metadata.UsageRecordId,
                Duration: overallStopwatch.ElapsedMilliseconds,
                Status: finalStatus,
                StatusDescription: finalStatusDescription,
                DownResponseBody: downResponseBody,
                InputTokens: finalUsage?.InputTokens,
                OutputTokens: finalUsage?.OutputTokens,
                CacheReadTokens: finalUsage?.CacheReadTokens,
                CacheCreationTokens: finalUsage?.CacheCreationTokens,
                AttemptCount: attemptNumber,
                DownStatusCode: finalDownStatusCode,
                DownRequestHeaders: finalStatus == UsageStatus.Failed ? downRequestHeaders : loggingDownRequestHeaders,
                DownRequestBody: finalStatus == UsageStatus.Failed ? downRequestBody : loggingDownRequestBody
            ));
        }
    }

    private async Task<RouteAccountSchedulingResult> SelectRouteAccountAsync(
        IReadOnlyList<RouteAccountSchedulingGroup> candidateGroups,
        string sessionHash,
        string? modelId,
        Dictionary<string, string> downHeaders,
        IReadOnlyCollection<Guid> excludedAccountIds,
        CancellationToken cancellationToken)
    {
        if (excludedAccountIds.Count >= _schedulingOptions.MaxAccountSwitches)
        {
            throw new ServiceUnavailableException($"已尝试 {_schedulingOptions.MaxAccountSwitches} 个账号，均不可用");
        }

        var candidateAccountIds = candidateGroups
            .SelectMany(group => group.CandidateRelations)
            .Select(relation => relation.AccountTokenId)
            .Where(accountId => !excludedAccountIds.Contains(accountId))
            .Distinct()
            .ToList();

        var concurrencyCountsTask = concurrencyStrategy.GetConcurrencyCountsAsync(candidateAccountIds);
        var rateLimitedIdsTask = rateLimitDomainService.GetRateLimitedAccountIdsAsync(candidateAccountIds, cancellationToken);
        await Task.WhenAll(concurrencyCountsTask, rateLimitedIdsTask);

        var schedulingContext = new RouteAccountSchedulingContext(sessionHash, modelId, excludedAccountIds);
        var schedulingState = new RouteAccountSchedulingStateSnapshot(
            concurrencyCountsTask.Result,
            rateLimitedIdsTask.Result);

        var result = await routeAccountSchedulingDomainService.ResolveBestAccountAsync(
            candidateGroups,
            schedulingContext,
            schedulingState,
            cancellationToken);

        if (result != null)
        {
            return result;
        }

        throw new ServiceUnavailableException($"当前候选范围中没有可用账号支持模型 {modelId}");
    }

    private async Task<IReadOnlyList<RouteAccountSchedulingGroup>> BuildSchedulingGroupsAsync(
        IReadOnlyList<(ProviderGroup Group, int Priority)> groups,
        IReadOnlyList<(Provider Provider, AuthMethod AuthMethod)>? allowedCombinations,
        CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
        {
            return [];
        }

        var groupIds = groups.Select(x => x.Group.Id).Distinct().ToList();
        var relations = await relationRepository.GetCandidatesByGroupIdsAsync(
            groupIds,
            allowedCombinations?.ToList(),
            cancellationToken);

        var relationsByGroupId = relations
            .GroupBy(x => x.ProviderGroupId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ProviderGroupAccountRelation>)x.ToList());

        return groups
            .Where(x => relationsByGroupId.ContainsKey(x.Group.Id))
            .Select(x => new RouteAccountSchedulingGroup(
                x.Group,
                relationsByGroupId[x.Group.Id],
                x.Priority))
            .ToList();
    }

    private async Task<(bool IsStreamCrash, string? StatusDesc, ResponseUsage? Usage, ModelErrorAnalysisResult? ErrorAnalysis)> HandleSuccessResponseAsync(
        IRouteResponseHandler responseHandler,
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
        ModelErrorAnalysisResult? errorAnalysis = null;
        var bufferedEvents = new List<(StreamEvent Event, byte[]? Bytes)>();

        if (!isCheckStreamHealth)
        {
            await HandleSuccessAsync(accountTokenId, upModelId, ct);
            await responseHandler.OnHeadersReadyAsync(proxyResponse.StatusCode, proxyResponse.Headers, ct);
            headersWritten = true;
        }

        try
        {
            await foreach (var evt in proxyResponse.Events!.WithCancellation(ct))
            {
                if (evt.IsComplete && evt.Usage != null)
                    usage = evt.Usage;

                if (isCheckStreamHealth && !headersWritten)
                {
                    if (evt.Type == StreamEventType.Error)
                    {
                        isStreamCrash = true;
                        errorAnalysis = accountRetryStrategyDomainService.AnalyzePreCommitStreamFailure(
                            isEmptyStream: false,
                            isHealthCheckError: true,
                            isIoException: false,
                            detail: $"流健康检查到内部错误事件节点 '{evt.Content ?? "unknown"}'");
                        statusDesc = errorAnalysis.Description;
                        AppendBodyLog(tempUpBody, evt.OriginalBytes, forceBodyCapture);
                        break;
                    }

                    if (evt.HasOutput)
                    {
                        await HandleSuccessAsync(accountTokenId, upModelId, ct);
                        await responseHandler.OnHeadersReadyAsync(proxyResponse.StatusCode, proxyResponse.Headers, ct);
                        headersWritten = true;

                        foreach (var bufferedEvent in bufferedEvents)
                        {
                            AppendBodyLog(tempDownBody, bufferedEvent.Bytes, forceBodyCapture);
                            await responseHandler.OnDataAsync(bufferedEvent.Event, bufferedEvent.Bytes, ct);
                        }
                        bufferedEvents.Clear();
                    }
                }

                var bytesToForward = evt.ConvertedBytes ?? evt.OriginalBytes;
                if (!responseHandler.ShouldHandle(evt, bytesToForward)) continue;

                AppendBodyLog(tempUpBody, evt.OriginalBytes, forceBodyCapture);

                if (isCheckStreamHealth && !headersWritten)
                {
                    bufferedEvents.Add((evt, bytesToForward));
                }
                else
                {
                    AppendBodyLog(tempDownBody, bytesToForward, forceBodyCapture);
                    await responseHandler.OnDataAsync(evt, bytesToForward, ct);
                }
            }

            if (isCheckStreamHealth && !headersWritten && !isStreamCrash)
            {
                isStreamCrash = true;
                errorAnalysis = accountRetryStrategyDomainService.AnalyzePreCommitStreamFailure(
                    isEmptyStream: true,
                    isHealthCheckError: false,
                    isIoException: false);
                statusDesc = errorAnalysis.Description;
            }

            if (isStreamCrash)
            {
                logger.LogWarning(statusDesc);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (responseHandler.HasResponseStarted)
            {
                throw;
            }

            isStreamCrash = true;
            errorAnalysis = accountRetryStrategyDomainService.AnalyzePreCommitStreamFailure(
                isEmptyStream: false,
                isHealthCheckError: false,
                isIoException: ex is IOException,
                detail: ex.Message);
            statusDesc = errorAnalysis.Description;
            logger.LogWarning(ex, statusDesc);
        }

        return (isStreamCrash, statusDesc, usage, errorAnalysis);
    }

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

    private string? LoggingSubBody(string? sourceStr, bool force = false)
    {
        if (string.IsNullOrEmpty(sourceStr)) return sourceStr;
        if (!force && !_loggingOptions.IsBodyLoggingEnabled) return null;
        var length = Math.Min(sourceStr.Length, _loggingOptions.MaxBodyLength);
        var content = sourceStr[..length];
        return sourceStr.Length > _loggingOptions.MaxBodyLength ? content + "...[Truncated]" : content;
    }

    private static string? CaptureHeaders(Dictionary<string, string> headers)
    {
        var filtered = headers
            .Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
                       !h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
            .Select(h => $"{h.Key}: {h.Value}");

        return string.Join("\n", filtered);
    }

    private void EnqueueAttemptStart(
        Guid usageRecordId,
        int attemptNumber,
        SelectAccountResultDto selectResult,
        string? upModelId,
        string? upUserAgent,
        string? upRequestUrl,
        string? upRequestHeaders,
        string? upRequestBody)
    {
        usageRecordQueue.TryEnqueue(new UsageRecordAttemptStartItem(
            UsageRecordId: usageRecordId,
            AttemptNumber: attemptNumber,
            AccountTokenId: selectResult.AccountToken.Id,
            AccountTokenName: selectResult.AccountToken.Name,
            Provider: selectResult.AccountToken.Provider,
            AuthMethod: selectResult.AccountToken.AuthMethod,
            ProviderGroupId: selectResult.ProviderGroupId,
            ProviderGroupName: selectResult.ProviderGroupName,
            GroupRateMultiplier: selectResult.GroupRateMultiplier,
            UpModelId: upModelId,
            UpUserAgent: upUserAgent,
            UpRequestUrl: upRequestUrl,
            UpRequestHeaders: upRequestHeaders,
            UpRequestBody: upRequestBody
        ));
    }

    private void EnqueueAttemptStart(
        Guid usageRecordId,
        int attemptNumber,
        AccountToken accountToken,
        ProviderGroup providerGroup,
        string? upModelId,
        string? upUserAgent,
        string? upRequestUrl,
        string? upRequestHeaders,
        string? upRequestBody)
    {
        usageRecordQueue.TryEnqueue(new UsageRecordAttemptStartItem(
            UsageRecordId: usageRecordId,
            AttemptNumber: attemptNumber,
            AccountTokenId: accountToken.Id,
            AccountTokenName: accountToken.Name,
            Provider: accountToken.Provider,
            AuthMethod: accountToken.AuthMethod,
            ProviderGroupId: providerGroup.Id,
            ProviderGroupName: providerGroup.Name,
            GroupRateMultiplier: providerGroup.RateMultiplier,
            UpModelId: upModelId,
            UpUserAgent: upUserAgent,
            UpRequestUrl: upRequestUrl,
            UpRequestHeaders: upRequestHeaders,
            UpRequestBody: upRequestBody
        ));
    }

    private void EnqueueAttemptEnd(
        Guid usageRecordId,
        int attemptNumber,
        int? upStatusCode,
        long durationMs,
        UsageStatus status,
        string? statusDescription,
        string? upResponseBody,
        string? upRequestHeaders,
        string? upRequestBody)
    {
        usageRecordQueue.TryEnqueue(new UsageRecordAttemptEndItem(
            UsageRecordId: usageRecordId,
            AttemptNumber: attemptNumber,
            UpStatusCode: upStatusCode,
            DurationMs: durationMs,
            Status: status,
            StatusDescription: statusDescription,
            UpResponseBody: upResponseBody,
            UpRequestHeaders: upRequestHeaders,
            UpRequestBody: upRequestBody
        ));
    }

    private async Task HandleSuccessAsync(
        Guid accountId,
        string? upModelId,
        CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account == null) return;

        if (account.RateLimitScope == RateLimitScope.Model && !string.IsNullOrWhiteSpace(upModelId))
        {
            await rateLimitDomainService.ClearModelBackoffCountAsync(accountId, upModelId, cancellationToken);
            await rateLimitDomainService.ClearModelLockAsync(accountId, upModelId, cancellationToken);

            if (account.ClearModelRateLimit(upModelId))
            {
                await accountRepository.UpdateAsync(account, cancellationToken);
            }

            return;
        }

        await rateLimitDomainService.ClearBackoffCountAsync(accountId, cancellationToken);

        if (account.ResetStatus())
        {
            await accountRepository.UpdateAsync(account, cancellationToken);
        }
    }

    private async Task HandleFailureAsync(
        HandleFailureInputDto input,
        CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(input.AccountId, cancellationToken);
        if (account == null) return;

        // 调用领域服务执行熔断/禁用
        await accountResultHandlerDomainService.HandleFailureAsync(
            account,
            input.StatusCode,
            input.ErrorContent,
            input.ErrorAnalysis.RetryType is RetryType.RetrySameAccount or RetryType.RetrySameAccountWithDowngrade,
            input.ErrorAnalysis.RetryAfter,
            input.DownModelId,
            input.UpModelId,
            cancellationToken);

        await accountRepository.UpdateAsync(account, cancellationToken);
    }

    private async Task<ConcurrencySlot> TryAcquireReadySlotAsync(
        SelectAccountResultDto selectResult,
        Guid activeRequestId,
        CancellationToken cancellationToken = default)
    {
        // 1. 熔断与限流检查
        if (await rateLimitDomainService.IsRateLimitedAsync(selectResult.AccountToken.Id, cancellationToken))
        {
            var description = $"账号 '{selectResult.AccountToken.Name}' 已处于限流/熔断状态，未发起上游请求，切换其他账号";
            logger.LogDebug("账号 {AccountName} 已被并发请求触发熔断，跳过并发槽位获取直接返回失败", selectResult.AccountToken.Name);
            return new ConcurrencySlot(false, failureDescription: description);
        }

        // 2. 尝试获取并发槽位
        bool acquired = await concurrencyStrategy.AcquireSlotAsync(
            selectResult.AccountToken.Id, activeRequestId, selectResult.WaitPlan.MaxConcurrency, cancellationToken);

        if (acquired) return new ConcurrencySlot(true, () => concurrencyStrategy.ReleaseSlotAsync(selectResult.AccountToken.Id, activeRequestId));
        if (accountRetryStrategyDomainService.DetermineConcurrencyFailureInstruction(
            selectResult.WaitPlan.ShouldWait,
            waitQueueFull: false,
            waitTimedOut: false) == FailureInstruction.SwitchAccount)
        {
            return new ConcurrencySlot(false,
                failureDescription: $"账号 '{selectResult.AccountToken.Name}' 当前无可用并发槽位，未进入等待直接切换其他账号");
        }

        // 3. 进入等待队列
        var maxWait = selectResult.WaitPlan.MaxConcurrency + _schedulingOptions.WaitQueueBufferSize;
        if (!await concurrencyStrategy.IncrementWaitCountAsync(selectResult.AccountToken.Id, maxWait, cancellationToken))
        {
            var instruction = accountRetryStrategyDomainService.DetermineConcurrencyFailureInstruction(
                selectResult.WaitPlan.ShouldWait,
                waitQueueFull: true,
                waitTimedOut: false);
            if (instruction == FailureInstruction.SwitchAccount)
            {
                logger.LogDebug("账号 {AccountName} 等待队列已满，放弃当前账号并尝试切换其他账号", selectResult.AccountToken.Name);
                return new ConcurrencySlot(false,
                    failureDescription: $"账号 '{selectResult.AccountToken.Name}' 等待队列已满，未发起上游请求，切换其他账号");
            }

            throw new ServiceUnavailableException("等待队列已满，请稍后重试");
        }

        try
        {
            acquired = await concurrencyStrategy.WaitForSlotAsync(
                selectResult.AccountToken.Id,
                activeRequestId,
                selectResult.WaitPlan.MaxConcurrency,
                selectResult.WaitPlan.Timeout,
                cancellationToken);
        }
        finally
        {
            await concurrencyStrategy.DecrementWaitCountAsync(selectResult.AccountToken.Id, cancellationToken);
        }

        if (!acquired)
        {
            var instruction = accountRetryStrategyDomainService.DetermineConcurrencyFailureInstruction(
                selectResult.WaitPlan.ShouldWait,
                waitQueueFull: false,
                waitTimedOut: true);
            if (instruction == FailureInstruction.SwitchAccount)
            {
                logger.LogDebug("账号 {AccountName} 等待并发槽位超时，放弃当前账号并尝试切换其他账号", selectResult.AccountToken.Name);
                return new ConcurrencySlot(false,
                    failureDescription: $"账号 '{selectResult.AccountToken.Name}' 等待并发槽位超时，未发起上游请求，切换其他账号");
            }

            throw new ServiceUnavailableException($"账号 {selectResult.AccountToken.Name} 繁忙，请稍后重试");
        }

        return new ConcurrencySlot(true, () => concurrencyStrategy.ReleaseSlotAsync(selectResult.AccountToken.Id, activeRequestId));
    }

    private (FailureInstruction Instruction, TimeSpan RetryDelay) DetermineFailureInstruction(
        ModelErrorAnalysisResult retryPolicy,
        int currentRetryCount,
        int maxRetries,
        int accountSwitchCount)
    {
        return accountRetryStrategyDomainService.DetermineFailureInstruction(
            retryPolicy, currentRetryCount, maxRetries, accountSwitchCount);
    }

    private static int ResolveInternalExceptionStatusCode(Exception exception)
    {
        if (exception is BusinessException biz)
        {
            var codeText = biz.Code.ToString();
            if (!string.IsNullOrEmpty(codeText) && codeText.Length >= 3)
            {
                return int.Parse(codeText[..3]);
            }
        }

        return exception switch
        {
            OperationCanceledException { InnerException: TimeoutException } => 503,
            TimeoutException => 503,
            HttpRequestException => 503,
            _ => 500
        };
    }
}

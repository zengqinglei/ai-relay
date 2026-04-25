using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiRelay.Application.ProviderAccounts.AppServices;
using AiRelay.Application.ProviderGroups.AppServices;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Application.UsageRecords.AppServices;
using AiRelay.Application.UsageRecords.Dtos.Lifecycle;
using AiRelay.Domain.ChatSessions.Entities;
using AiRelay.Domain.ChatSessions.ValueObjects;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.UsageRecords.Entities;
using AiRelay.Domain.UsageRecords.ValueObjects;
using Leistd.Ddd.Application.AppService;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ChatSessions.AppServices;

/// <summary>
/// 工作区聊天执行编排服务
/// </summary>
public class WorkspaceChatExecutionAppService(
    IProviderGroupRepository providerGroupRepository,
    IProviderGroupAccountRelationRepository relationRepository,
    ProviderGroupDomainService providerGroupDomainService,
    AccountTokenDomainService accountTokenDomainService,
    ISmartProxyAppService smartProxyAppService,
    IChatModelHandlerFactory chatModelHandlerFactory,
    IConcurrencyStrategy concurrencyStrategy,
    AccountFingerprintAppService fingerprintAppService,
    IUsageLifecycleAppService usageLifecycleAppService,
    ILogger<WorkspaceChatExecutionAppService> logger) : BaseAppService, IWorkspaceChatExecutionAppService
{
    public async IAsyncEnumerable<StreamEvent> ExecuteAsync(
        ChatSession session,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var context = new WorkspaceChatExecutionContext();
        await usageLifecycleAppService.StartUsageAsync(
            new StartUsageInputDto(
                context.UsageRecordId,
                session.UserId,
                UsageSource.WorkspaceChat,
                context.CorrelationId,
                session.Id.ToString("N"),
                null,
                null,
                true,
                "POST",
                $"/api/v1/chat-sessions/{session.Id}/messages",
                session.ModelId,
                null,
                null,
                null,
                session.Messages.LastOrDefault(x => x.Role == ChatMessageRole.User)?.Content),
            cancellationToken);

        StreamEvent? terminalError = null;
        var enumerator = ExecuteCoreAsync(session, context, cancellationToken).GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                StreamEvent current;

                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    current = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    context.FinalStatusDescription = "请求已取消";
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "工作区聊天请求执行失败: SessionId={SessionId}", session.Id);
                    context.FinalStatus = UsageStatus.Failed;
                    context.FinalStatusDescription = $"工作区聊天请求执行失败: {ex.Message}";
                    context.UpResponseBody ??= $"{ex.GetType().Name}: {ex.Message}";
                    terminalError = new StreamEvent
                    {
                        Type = StreamEventType.Error,
                        Content = context.FinalStatusDescription,
                        IsComplete = true
                    };
                    break;
                }

                yield return current;
            }
        }
        finally
        {
            if (context.AccountId.HasValue)
            {
                await usageLifecycleAppService.CompleteAttemptAsync(
                    new CompleteAttemptInputDto(
                        context.UsageRecordId,
                        context.AttemptNumber,
                        context.UpStatusCode,
                        (long)(DateTime.UtcNow - context.AttemptStartedAt).TotalMilliseconds,
                        context.FinalStatus,
                        context.FinalStatusDescription,
                        context.UpResponseBody,
                        context.UpRequestHeaders,
                        context.UpRequestBody),
                    CancellationToken.None);
            }

            await usageLifecycleAppService.FinishUsageAsync(
                new FinishUsageInputDto(
                    context.UsageRecordId,
                    (long)(DateTime.UtcNow - context.StartedAt).TotalMilliseconds,
                    context.FinalStatus,
                    context.FinalStatusDescription,
                    context.AssistantBody.Length > 0 ? context.AssistantBody.ToString() : context.FinalStatusDescription,
                    context.Usage?.InputTokens,
                    context.Usage?.OutputTokens,
                    context.Usage?.CacheReadTokens,
                    context.Usage?.CacheCreationTokens,
                    context.AccountId.HasValue ? 1 : 0,
                    context.DownStatusCode,
                    null,
                    null),
                CancellationToken.None);
        }

        if (terminalError != null)
        {
            yield return terminalError;
        }
    }

    private async IAsyncEnumerable<StreamEvent> ExecuteCoreAsync(
        ChatSession session,
        WorkspaceChatExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var resolved = await ResolveAccountAsync(session, cancellationToken);
        var account = resolved.AccountToken;
        var group = resolved.ProviderGroup;

        context.AccountId = account.Id;
        context.ProviderGroupId = group.Id;
        context.ProviderGroupName = group.Name;

        if (account.AuthMethod != AuthMethod.ApiKey && string.IsNullOrWhiteSpace(account.AccessToken))
        {
            throw new UnauthorizedException($"{account.Provider} 账户 '{account.Name}' 的凭证为空");
        }

        if (await smartProxyAppService.IsRateLimitedAsync(account.Id, cancellationToken))
        {
            throw new ServiceUnavailableException($"账号 {account.Name} 当前处于限流状态，请稍后重试");
        }

        var activeRequestId = Guid.CreateVersion7();
        var slotAcquired = false;

        try
        {
            slotAcquired = await TryAcquireConcurrencySlotAsync(account, activeRequestId, cancellationToken);
            if (!slotAcquired)
            {
                throw new ServiceUnavailableException($"账号 {account.Name} 当前无可用并发槽位");
            }

            var handler = chatModelHandlerFactory.CreateHandler(
                account.Provider,
                account.AuthMethod,
                account.AccessToken!,
                account.BaseUrl,
                account.ExtraProperties,
                account.AllowOfficialClientMimic,
                account.ModelWhites,
                account.ModelMapping);
            var downContext = handler.CreateChatDownContext(MapToChatDownContextInput(session, account));
            await SetupFingerprintIfRequiredAsync(downContext, account, cancellationToken);
            var upContext = await handler.ProcessRequestContextAsync(downContext, 0, cancellationToken);
            var upModelId = upContext.MappedModelId ?? downContext.ModelId;
            context.UpRequestHeaders = CaptureHeaders(upContext.Headers);
            context.UpRequestBody = upContext.GetBodyPreview(downContext.PreloadedBodyPreview, 4000);
            context.AttemptStartedAt = DateTime.UtcNow;

            await usageLifecycleAppService.StartAttemptAsync(
                new StartAttemptInputDto(
                    context.UsageRecordId,
                    context.AttemptNumber,
                    account.Id,
                    account.Name,
                    account.Provider,
                    account.AuthMethod,
                    group.Id,
                    group.Name,
                    group.RateMultiplier,
                    upModelId,
                    upContext.GetUserAgent(),
                    upContext.GetFullUrl(),
                    context.UpRequestHeaders,
                    context.UpRequestBody),
                cancellationToken);

            var proxyResponse = await handler.SendChatRequestAsync(upContext, downContext, true, cancellationToken);
            context.UpStatusCode = proxyResponse.StatusCode;

            if (!proxyResponse.IsSuccess)
            {
                var errorEvent = CreateErrorEvent(proxyResponse.ErrorBody, proxyResponse.StatusCode);
                context.FinalStatus = UsageStatus.Failed;
                context.UpResponseBody = proxyResponse.ErrorBody;
                context.DownStatusCode = proxyResponse.StatusCode;
                context.FinalStatusDescription = errorEvent.Content;

                var retryPolicy = await handler.CheckRetryPolicyAsync(
                    proxyResponse.StatusCode,
                    downContext.RelativePath,
                    proxyResponse.Headers,
                    proxyResponse.ErrorBody);

                if (retryPolicy.RetryType != RetryType.UnsupportedEndpoint)
                {
                    await smartProxyAppService.HandleFailureAsync(
                        new HandleFailureInputDto(
                            account.Id,
                            proxyResponse.StatusCode,
                            proxyResponse.ErrorBody,
                            session.ModelId,
                            upModelId,
                            retryPolicy),
                        CancellationToken.None);
                }

                yield return errorEvent;
                yield break;
            }

            var healthCheckPassed = !account.IsCheckStreamHealth;
            if (healthCheckPassed)
            {
                await smartProxyAppService.HandleSuccessAsync(account.Id, upModelId, cancellationToken);
            }

            await foreach (var evt in proxyResponse.Events!.WithCancellation(cancellationToken))
            {
                if (evt.IsComplete && evt.Usage != null)
                {
                    context.Usage = evt.Usage;
                }

                if (!healthCheckPassed)
                {
                    if (evt.Type == StreamEventType.Error)
                    {
                        context.FinalStatus = UsageStatus.Failed;
                        context.FinalStatusDescription = $"流健康检查到内部错误事件节点 '{evt.Content ?? "unknown"}'";
                        context.UpResponseBody ??= evt.Content;
                        context.DownStatusCode ??= 200;
                        yield return new StreamEvent
                        {
                            Type = StreamEventType.Error,
                            Content = context.FinalStatusDescription,
                            IsComplete = true
                        };
                        yield break;
                    }

                    if (evt.HasOutput)
                    {
                        healthCheckPassed = true;
                        await smartProxyAppService.HandleSuccessAsync(account.Id, upModelId, cancellationToken);
                    }
                }

                if (evt.Type == StreamEventType.Content && !string.IsNullOrEmpty(evt.Content))
                {
                    context.AssistantBody.Append(evt.Content);
                }

                if (evt.Type == StreamEventType.Error)
                {
                    context.FinalStatus = UsageStatus.Failed;
                    context.FinalStatusDescription = evt.Content ?? "上游返回错误事件";
                    context.UpResponseBody ??= evt.Content;
                    context.DownStatusCode ??= 200;
                    yield return evt;
                    yield break;
                }

                if (evt.Content != null || evt.Type == StreamEventType.System || evt.IsComplete || evt.InlineData != null)
                {
                    yield return evt;
                }
            }

            if (!healthCheckPassed)
            {
                context.FinalStatus = UsageStatus.Failed;
                context.FinalStatusDescription = "流健康检查未读取到包含有效文本，判定为空流或无响应";
                context.UpResponseBody ??= context.FinalStatusDescription;
                context.DownStatusCode ??= 200;
                yield return new StreamEvent
                {
                    Type = StreamEventType.Error,
                    Content = context.FinalStatusDescription,
                    IsComplete = true
                };
                yield break;
            }

            context.FinalStatus = UsageStatus.Success;
            context.DownStatusCode = 200;
        }
        finally
        {
            if (slotAcquired)
            {
                await concurrencyStrategy.ReleaseSlotAsync(account.Id, activeRequestId);
            }
        }
    }

    private async Task<(AccountToken AccountToken, ProviderGroup ProviderGroup)> ResolveAccountAsync(
        ChatSession session,
        CancellationToken cancellationToken)
    {
        if (!session.ProviderGroupId.HasValue)
        {
            if (session.AccountId.HasValue)
            {
                throw new BadRequestException("固定账户模式下必须显式选择资源池分组");
            }

            var visibleGroups = await providerGroupRepository.GetVisibleGroupsAsync(session.UserId, cancellationToken);
            foreach (var visibleGroup in visibleGroups)
            {
                var selectedAccount = await providerGroupDomainService.SelectAccountFromGroupAsync(
                    visibleGroup,
                    session.Id.ToString("N"),
                    requestedModel: session.ModelId);

                if (selectedAccount?.AccountToken == null)
                {
                    continue;
                }

                await accountTokenDomainService.RefreshTokenIfNeededAsync(selectedAccount.Value.AccountToken, cancellationToken);
                return (selectedAccount.Value.AccountToken, selectedAccount.Value.Group);
            }

            throw new ServiceUnavailableException($"当前可见资源池中没有可用账号支持模型 {session.ModelId}");
        }

        if (session.AccountId.HasValue)
        {
            var candidates = await relationRepository.GetCandidatesAsync(session.ProviderGroupId.Value, cancellationToken: cancellationToken);
            var relation = candidates.FirstOrDefault(x => x.AccountTokenId == session.AccountId.Value);

            if (relation?.AccountToken == null)
            {
                throw new NotFoundException($"固定账户不存在或未绑定到资源池: {session.AccountId}");
            }

            await accountTokenDomainService.RefreshTokenIfNeededAsync(relation.AccountToken, cancellationToken);
            return (relation.AccountToken, relation.ProviderGroup);
        }

        var providerGroup = await providerGroupRepository.GetVisibleByIdAsync(session.ProviderGroupId.Value, session.UserId, cancellationToken)
            ?? throw new NotFoundException($"资源池不存在: {session.ProviderGroupId}");

        var result = await providerGroupDomainService.SelectAccountFromGroupAsync(
            providerGroup,
            session.Id.ToString("N"),
            requestedModel: session.ModelId);

        if (result?.AccountToken == null)
        {
            throw new ServiceUnavailableException($"资源池 '{providerGroup.Name}' 中没有可用账号支持模型 {session.ModelId}");
        }

        await accountTokenDomainService.RefreshTokenIfNeededAsync(result.Value.AccountToken, cancellationToken);
        return (result.Value.AccountToken, result.Value.Group);
    }

    private async Task SetupFingerprintIfRequiredAsync(
        DownRequestContext downContext,
        AccountToken account,
        CancellationToken cancellationToken)
    {
        if (!account.AllowOfficialClientMimic)
        {
            return;
        }

        downContext.StickySessionId = await fingerprintAppService.GenerateSessionUuidAsync(
            account.Id,
            downContext.SessionId,
            account.ExtraProperties.TryGetValue("session_id_masking_enabled", out var maskingValue)
                && bool.TryParse(maskingValue, out var enabled)
                && enabled,
            cancellationToken);

        var fingerprint = await fingerprintAppService.GetOrCreateFingerprintAsync(
            account.Id,
            downContext.Headers,
            cancellationToken);
        downContext.FingerprintClientId = fingerprint.ClientId;
    }

    private async Task<bool> TryAcquireConcurrencySlotAsync(
        AccountToken account,
        Guid activeRequestId,
        CancellationToken cancellationToken)
    {
        if (account.MaxConcurrency <= 0)
        {
            return true;
        }

        return await concurrencyStrategy.AcquireSlotAsync(
            account.Id,
            activeRequestId,
            account.MaxConcurrency,
            cancellationToken);
    }

    private static ChatDownContextInput MapToChatDownContextInput(ChatSession session, AccountToken account)
    {
        return new ChatDownContextInput(
            session.ModelId,
            session.Id.ToString("N"),
            session.Messages
                .OrderBy(message => message.CreationTime)
                .ThenBy(message => message.Id)
                .Select(message => new ChatDownContextMessageInput(
                    message.Role switch
                    {
                        ChatMessageRole.System => ChatDownContextMessageRole.System,
                        ChatMessageRole.Assistant => ChatDownContextMessageRole.Assistant,
                        _ => ChatDownContextMessageRole.User
                    },
                    message.Content,
                    message.Attachments.Select(attachment => new ChatDownContextAttachmentInput(
                        attachment.MimeType,
                        attachment.Url,
                        attachment.Data)).ToArray()))
                .ToArray(),
            Stream: true,
            MaxTokens: 4096,
            DisableStore: account.AuthMethod == AuthMethod.OAuth);
    }

    private static StreamEvent CreateErrorEvent(string? content, int statusCode)
    {
        var message = string.IsNullOrWhiteSpace(content)
            ? $"上游请求失败，状态码: {statusCode}"
            : $"上游请求失败，状态码: {statusCode}，详情: {content}";

        return new StreamEvent
        {
            Type = StreamEventType.Error,
            Content = message,
            IsComplete = true
        };
    }

    private static string? CaptureHeaders(Dictionary<string, string> headers)
    {
        var filtered = headers
            .Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
                        !h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
            .Select(h => $"{h.Key}: {h.Value}");

        return string.Join("\n", filtered);
    }

    private sealed class WorkspaceChatExecutionContext
    {
        public Guid UsageRecordId { get; } = Guid.CreateVersion7();

        public string CorrelationId { get; } = Guid.CreateVersion7().ToString("N");

        public int AttemptNumber { get; } = 1;

        public DateTime StartedAt { get; } = DateTime.UtcNow;

        public DateTime AttemptStartedAt { get; set; } = DateTime.UtcNow;

        public StringBuilder AssistantBody { get; } = new();

        public ResponseUsage? Usage { get; set; }

        public UsageStatus FinalStatus { get; set; } = UsageStatus.Failed;

        public string? FinalStatusDescription { get; set; }

        public int? DownStatusCode { get; set; }

        public int? UpStatusCode { get; set; }

        public string? UpResponseBody { get; set; }

        public string? UpRequestHeaders { get; set; }

        public string? UpRequestBody { get; set; }

        public Guid? AccountId { get; set; }

        public Guid? ProviderGroupId { get; set; }

        public string? ProviderGroupName { get; set; }
    }
}





using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AiRelay.Application.ApiKeys.Options;
using AiRelay.Application.ChatSessions.Dtos;
using AiRelay.Application.ChatSessions.Handlers;
using AiRelay.Application.ModelRoutes;
using AiRelay.Application.ModelRoutes.Dtos;
using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ChatSessions.Entities;
using AiRelay.Domain.ChatSessions.ValueObjects;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.UsageRecords.ValueObjects;
using Leistd.Ddd.Application.AppService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.ChatSessions.AppServices;

/// <summary>
/// 工作区聊天执行编排服务
/// </summary>
public class WorkspaceChatExecutionAppService(
    IModelRouteAppService modelRouteAppService,
    IChatModelHandlerFactory chatModelHandlerFactory,
    ILogger<WorkspaceChatExecutionAppService> logger,
    RouteAccountSchedulingDomainService schedulingDomainService,
    IOptions<DefaultProviderModelsOptions> defaultProviderModelsOptions) : BaseAppService, IWorkspaceChatExecutionAppService
{
    public async IAsyncEnumerable<StreamEvent> ExecuteAsync(
        ChatSession session,
        WorkspaceChatRequestContextDto requestContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var channel = Channel.CreateBounded<StreamEvent>(new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var responseHandler = new ChatRouteResponseHandler(channel);

        var baseDownContext = new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = $"/api/v1/chat-sessions/{session.Id}/messages",
            IsStreaming = true,
            ModelId = session.ModelId,
            SessionId = session.Id.ToString("N"),
            DownRequestUrl = requestContext.RequestUrl,
            PreloadedBodyPreview = session.Messages.LastOrDefault(x => x.Role == ChatMessageRole.User)?.Content,
            ClientIp = requestContext.ClientIp,
            Headers = new Dictionary<string, string>(requestContext.Headers, StringComparer.OrdinalIgnoreCase)
        };

        // auto 模型解析：优先使用粘性缓存的上次成功模型，否则从候选列表第一个开始，后续由 failover 机制兜底
        ModelFailoverContext? failoverContext = null;
        if (string.Equals(session.ModelId, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var models = defaultProviderModelsOptions.Value.Models
                .Where(m => !string.Equals(m, "auto", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (models.Length == 0)
                throw new InvalidOperationException("DefaultProviderModels 未配置任何有效的候选模型（排除 auto 后为空）");

            var startIndex = 0;
            var cachedModel = await schedulingDomainService.GetAutoModelAsync(baseDownContext.SessionId!, cancellationToken);
            if (cachedModel != null)
            {
                var idx = Array.FindIndex(models, m => string.Equals(m, cachedModel, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) startIndex = idx;
            }

            baseDownContext.ModelId = "auto";
            baseDownContext.ResolvedModelId = models[startIndex];
            failoverContext = new ModelFailoverContext
            {
                CandidateModels = models,
                CurrentModelIndex = startIndex
            };
        }

        var metadata = new RouteExecutionMetadata(
            UsageRecordId: Guid.CreateVersion7(),
            UserId: session.UserId,
            Source: UsageSource.WorkspaceChat,
            CorrelationId: Guid.CreateVersion7().ToString("N"),
            ApiKeyId: null,
            ApiKeyName: null);

        var candidateGroups = await modelRouteAppService.ResolveWorkspaceRouteCandidatesAsync(new SelectWorkspaceAccountInputDto
        {
            UserId = session.UserId,
            SessionId = session.Id,
            ProviderGroupId = session.ProviderGroupId,
            AccountId = session.AccountId,
            ModelId = session.ModelId
        }, cancellationToken);

        Func<SelectAccountResultDto, DownRequestContext> downContextModifier = selectResult =>
        {
            var handler = chatModelHandlerFactory.CreateHandler(
                selectResult.AccountToken.Provider,
                selectResult.AccountToken.AuthMethod,
                selectResult.AccountToken.AccessToken,
                selectResult.AccountToken.BaseUrl,
                selectResult.AccountToken.ExtraProperties,
                selectResult.AccountToken.AllowOfficialClientMimic,
                selectResult.AccountToken.ModelWhites,
                selectResult.AccountToken.ModelMapping);

            var downContext = handler.CreateChatDownContext(MapToChatDownContextInput(session, selectResult.AccountToken));

            downContext.ModelId = session.ModelId;
            downContext.SessionId = baseDownContext.SessionId;
            downContext.StickySessionId = baseDownContext.StickySessionId;
            downContext.FingerprintClientId = baseDownContext.FingerprintClientId;

            foreach (var (key, value) in baseDownContext.Headers)
            {
                if (key.Equals("content-length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                downContext.Headers[key] = value;
            }

            downContext.Headers["content-type"] = "application/json";

            return downContext;
        };

        var executeTask = ExecuteRouteAndCompleteChannelAsync();

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(linkedCts.Token))
            {
                yield return evt;
            }
            await executeTask;
        }
        finally
        {
            if (!executeTask.IsCompleted)
            {
                linkedCts.Cancel();
            }

            try
            {
                await executeTask;
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                // 调用方提前停止消费或显式取消时，确保后台执行链正常收敛。
            }
        }

        async Task ExecuteRouteAndCompleteChannelAsync()
        {
            try
            {
                await modelRouteAppService.ExecuteRouteAsync(
                    baseDownContext, metadata, candidateGroups, downContextModifier, responseHandler,
                    failoverContext, linkedCts.Token);

                if (failoverContext != null && baseDownContext.ResolvedModelId != null)
                {
                    await schedulingDomainService.SetAutoModelAsync(
                        baseDownContext.SessionId!, baseDownContext.ResolvedModelId, linkedCts.Token);
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation(ex, "客户端主动断开: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "工作区聊天执行异常: {Message}", ex.Message);
                await channel.Writer.WriteAsync(new StreamEvent
                {
                    Type = StreamEventType.Error,
                    Content = $"系统内部执行异常: {ex.Message}",
                    IsComplete = true
                }, CancellationToken.None);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }
    }

    private static ChatDownContextInput MapToChatDownContextInput(ChatSession session, AvailableAccountTokenOutputDto account)
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
}

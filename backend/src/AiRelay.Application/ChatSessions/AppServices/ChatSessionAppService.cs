using System.Runtime.CompilerServices;
using System.Text;
using AiRelay.Application.ChatSessions.Dtos;
using AiRelay.Domain.ChatSessions.Entities;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Leistd.Security.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ChatSessions.AppServices;

/// <summary>
/// 工作区聊天会话应用服务
/// </summary>
public class ChatSessionAppService(
    IRepository<ChatSession, Guid> chatSessionRepository,
    IRepository<ChatMessage, Guid> chatMessageRepository,
    IProviderGroupRepository providerGroupRepository,
    IProviderGroupAccountRelationRepository relationRepository,
    IWorkspaceChatExecutionAppService workspaceChatExecutionAppService,
    AccountTokenDomainService accountTokenDomainService,
    IModelProvider modelProvider,
    IObjectMapper objectMapper,
    IMemoryCache memoryCache,
    ICurrentUser currentUser,
    IQueryableAsyncExecuter asyncExecuter,
    ILogger<ChatSessionAppService> logger) : BaseAppService, IChatSessionAppService
{
    private static readonly TimeSpan ModelOptionsCacheDuration = TimeSpan.FromMinutes(5);

    public async Task<List<ChatSessionOutputDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id!.Value;
        var query = await chatSessionRepository.GetQueryableAsync(cancellationToken);
        var sessions = await asyncExecuter.ToListAsync(
            query
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.LastMessageTime ?? x.CreationTime)
                .ThenByDescending(x => x.CreationTime),
            cancellationToken);

        return objectMapper.Map<List<ChatSession>, List<ChatSessionOutputDto>>(sessions);
    }

    public async Task<ChatSessionOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(id, includeMessages: false, cancellationToken);
        return objectMapper.Map<ChatSession, ChatSessionOutputDto>(session);
    }

    public async Task<PagedResultDto<ChatMessageOutputDto>> GetMessagePagedListAsync(
        Guid id,
        GetChatMessagePagedInputDto input,
        CancellationToken cancellationToken = default)
    {
        _ = await GetOwnedSessionAsync(id, includeMessages: false, cancellationToken);

        var limit = Math.Clamp(input.Limit, 1, 100);
        var query = await chatMessageRepository.GetQueryIncludingAsync(cancellationToken, x => x.Attachments);
        query = query.Where(x => x.SessionId == id);

        if (input.CursorMessageId.HasValue)
        {
            var cursor = await asyncExecuter.SingleOrDefaultAsync(
                query
                    .Where(x => x.Id == input.CursorMessageId.Value)
                    .Select(x => new ChatMessageCursorItem(x.CreationTime, x.Id)),
                cancellationToken);

            if (cursor == null)
            {
                return new PagedResultDto<ChatMessageOutputDto>(0, []);
            }

            query = query.Where(x => x.CreationTime < cursor.CreationTime);
        }

        var totalCount = await asyncExecuter.CountAsync(query, cancellationToken);
        var items = await asyncExecuter.ToListAsync(
            query
                .OrderByDescending(x => x.CreationTime)
                .ThenByDescending(x => x.Id)
                .Take(limit),
            cancellationToken);

        items.Reverse();
        var outputItems = objectMapper.Map<List<ChatMessage>, List<ChatMessageOutputDto>>(items);
        return new PagedResultDto<ChatMessageOutputDto>(totalCount, outputItems);
    }

    public async Task<ChatSessionOutputDto> CreateAsync(CreateChatSessionInputDto input, CancellationToken cancellationToken = default)
    {
        if (input.ProviderGroupId.HasValue)
        {
            await EnsureProviderGroupAsync(input.ProviderGroupId.Value, cancellationToken);
        }

        if (input.AccountId.HasValue)
        {
            if (!input.ProviderGroupId.HasValue)
            {
                throw new BadRequestException("固定账户模式下必须显式选择资源池分组");
            }

            await EnsureAccountBelongsToGroupAsync(input.ProviderGroupId.Value, input.AccountId.Value, cancellationToken);
        }

        var session = new ChatSession(
            currentUser.Id!.Value,
            input.Title ?? "新会话",
            input.ProviderGroupId,
            input.ModelId,
            input.AccountId);

        await chatSessionRepository.InsertAsync(session, cancellationToken);
        logger.LogInformation("创建工作区聊天会话成功: SessionId={SessionId}", session.Id);
        return objectMapper.Map<ChatSession, ChatSessionOutputDto>(session);
    }

    public async Task<ChatSessionOutputDto> UpdateAsync(Guid id, UpdateChatSessionInputDto input, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(id, includeMessages: false, cancellationToken);
        var providerGroupId = input.UseAutoProviderGroup ? null : input.ProviderGroupId ?? session.ProviderGroupId;
        var accountId = input.AccountId ?? session.AccountId;

        if (input.ProviderGroupId.HasValue)
        {
            await EnsureProviderGroupAsync(providerGroupId!.Value, cancellationToken);
        }

        if (accountId.HasValue)
        {
            if (!providerGroupId.HasValue)
            {
                throw new BadRequestException("固定账户模式下必须显式选择资源池分组");
            }

            await EnsureAccountBelongsToGroupAsync(providerGroupId.Value, accountId.Value, cancellationToken);
        }

        session.Update(input.Title, input.ProviderGroupId, input.ModelId, input.AccountId, input.UseAutoProviderGroup);
        await chatSessionRepository.UpdateAsync(session, cancellationToken: cancellationToken);

        logger.LogInformation("更新工作区聊天会话成功: SessionId={SessionId}", session.Id);
        return objectMapper.Map<ChatSession, ChatSessionOutputDto>(session);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(id, includeMessages: false, cancellationToken);
        await chatSessionRepository.DeleteAsync(session, cancellationToken: cancellationToken);
        logger.LogInformation("删除工作区聊天会话成功: SessionId={SessionId}", session.Id);
    }

    public async Task<IReadOnlyList<ChatModelOptionOutputDto>> GetModelOptionsAsync(
        Guid? providerGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = providerGroupId.HasValue
            ? $"workspace-chat:model-options:{providerGroupId.Value:N}"
            : $"workspace-chat:model-options:all:{currentUser.Id!.Value:N}";

        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyList<ChatModelOptionOutputDto>? cached) && cached != null)
        {
            return cached;
        }

        List<AccountToken> accounts;
        HashSet<Guid> visibleGroupIds = [];
        // accountId → 首个可见 groupId 的 lookup（仅无指定 providerGroupId 时使用）
        Dictionary<Guid, Guid> accountToGroupLookup = [];
        Dictionary<Guid, ProviderGroup> groupLookup;
        if (providerGroupId.HasValue)
        {
            await EnsureProviderGroupAsync(providerGroupId.Value, cancellationToken);

            var candidates = await relationRepository.GetCandidatesAsync(providerGroupId.Value, cancellationToken: cancellationToken);
            accounts = candidates
                .Where(x => x.AccountToken != null)
                .Select(x => x.AccountToken!)
                .DistinctBy(x => x.Id)
                .ToList();

            var group = await providerGroupRepository.GetVisibleByIdAsync(providerGroupId.Value, currentUser.Id!.Value, cancellationToken)
                ?? throw new NotFoundException($"资源池不存在: {providerGroupId.Value}");
            groupLookup = new Dictionary<Guid, ProviderGroup>(1) { [group.Id] = group };
        }
        else
        {
            var visibleGroups = await providerGroupRepository.GetVisibleGroupsAsync(currentUser.Id!.Value, cancellationToken);
            visibleGroupIds = visibleGroups.Select(x => x.Id).ToHashSet();
            groupLookup = visibleGroups.ToDictionary(x => x.Id);

            var relationQuery = await relationRepository.GetQueryableAsync(cancellationToken);
            var visibleRelations = await asyncExecuter.ToListAsync(
                relationQuery
                    .Include(x => x.AccountToken)
                    .Where(x => visibleGroupIds.Contains(x.ProviderGroupId) && x.IsActive && x.AccountToken != null && x.AccountToken.IsActive),
                cancellationToken);

            accounts = visibleRelations
                .Select(x => x.AccountToken!)
                .DistinctBy(x => x.Id)
                .ToList();

            // 构建 accountId → 首个可见 groupId 映射，避免内层循环逐条查库
            accountToGroupLookup = visibleRelations
                .GroupBy(x => x.AccountTokenId)
                .ToDictionary(g => g.Key, g => g.First().ProviderGroupId);
        }

        var result = new Dictionary<string, ChatModelOptionOutputDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in accounts.Where(x => x.IsAvailable()))
        {
            foreach (var model in await ResolveModelOptionsAsync(account, cancellationToken))
            {
                if (!await IsModelCurrentlyRoutableAsync(account, model.Value, cancellationToken))
                {
                    continue;
                }

                if (result.ContainsKey(model.Value))
                {
                    continue;
                }

                Guid? resolvedGroupId = providerGroupId;
                string? resolvedGroupName = providerGroupId.HasValue
                    ? groupLookup.GetValueOrDefault(providerGroupId.Value)?.Name
                    : null;

                if (!providerGroupId.HasValue)
                {
                    var matchedGroupId = accountToGroupLookup.GetValueOrDefault(account.Id);
                    resolvedGroupId = matchedGroupId != Guid.Empty ? matchedGroupId : null;
                    resolvedGroupName = resolvedGroupId.HasValue
                        ? groupLookup.GetValueOrDefault(resolvedGroupId.Value)?.Name
                        : null;
                }

                model.ProviderGroupId = resolvedGroupId;
                model.ProviderGroupName = resolvedGroupName;
                result[model.Value] = model;
            }
        }

        var modelOrderMap = BuildModelOrderMap();
        var output = result.Values
            .OrderBy(x => modelOrderMap.GetValueOrDefault(x.Value, int.MaxValue))
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        memoryCache.Set(cacheKey, output, ModelOptionsCacheDuration);
        return output;
    }

    public async IAsyncEnumerable<StreamEvent> SendMessageAsync(
        Guid id,
        SendChatMessageInputDto input,
        WorkspaceChatRequestContextDto requestContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(id, includeMessages: true, cancellationToken);
        var userMessage = session.CreateUserMessage(input.Content, input.Attachments);

        await chatSessionRepository.UpdateAsync(session, cancellationToken: cancellationToken);
        await chatMessageRepository.InsertAsync(userMessage, cancellationToken: cancellationToken);
        session.Messages.Add(userMessage);

        var assistantContent = new StringBuilder();
        var assistantReasoningContent = new StringBuilder();
        List<InlineDataPart>? assistantAttachments = null;
        var hasError = false;

        await foreach (var evt in workspaceChatExecutionAppService.ExecuteAsync(session, requestContext, cancellationToken))
        {
            if (evt.Type == StreamEventType.Content && !string.IsNullOrEmpty(evt.Content))
            {
                assistantContent.Append(evt.Content);
            }

            if (evt.Type == StreamEventType.Content && !string.IsNullOrEmpty(evt.ReasoningContent))
            {
                assistantReasoningContent.Append(evt.ReasoningContent);
            }

            if (evt.InlineData is { Count: > 0 })
            {
                assistantAttachments ??= [];
                assistantAttachments.AddRange(evt.InlineData);
            }

            if (evt.Type == StreamEventType.Error)
            {
                hasError = true;
            }

            yield return evt;
        }

        if (!hasError && (assistantContent.Length > 0 || assistantReasoningContent.Length > 0 || assistantAttachments is { Count: > 0 }))
        {
            var assistantMessage = session.CreateAssistantMessage(
                assistantContent.ToString(),
                assistantAttachments,
                assistantReasoningContent.Length > 0 ? assistantReasoningContent.ToString() : null);

            await chatSessionRepository.UpdateAsync(session, cancellationToken: cancellationToken);
            await chatMessageRepository.InsertAsync(assistantMessage, cancellationToken: cancellationToken);
            session.Messages.Add(assistantMessage);
        }
    }

    private async Task<ChatSession> GetOwnedSessionAsync(Guid id, bool includeMessages, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id!.Value;
        IQueryable<ChatSession> query;

        if (includeMessages)
        {
            query = (await chatSessionRepository.GetQueryableAsync(cancellationToken))
                .Include(x => x.Messages.OrderBy(m => m.CreationTime).ThenBy(m => m.Id))
                    .ThenInclude(x => x.Attachments)
                .AsSplitQuery();
        }
        else
        {
            query = await chatSessionRepository.GetQueryableAsync(cancellationToken);
        }

        var session = await asyncExecuter.SingleOrDefaultAsync(
            query.Where(x => x.Id == id && x.UserId == userId),
            cancellationToken);

        if (session == null)
        {
            throw new UnauthorizedException($"会话不存在: {id}");
        }

        return session;
    }

    private async Task EnsureProviderGroupAsync(Guid providerGroupId, CancellationToken cancellationToken)
    {
        if (await providerGroupRepository.GetVisibleByIdAsync(providerGroupId, currentUser.Id!.Value, cancellationToken) == null)
        {
            throw new NotFoundException($"资源池不存在: {providerGroupId}");
        }
    }

    private async Task EnsureAccountBelongsToGroupAsync(Guid providerGroupId, Guid accountId, CancellationToken cancellationToken)
    {
        var relationQuery = await relationRepository.GetQueryableAsync(cancellationToken);
        var exists = await asyncExecuter.AnyAsync(
            relationQuery.Where(x => x.ProviderGroupId == providerGroupId && x.AccountTokenId == accountId && x.IsActive),
            cancellationToken);

        if (!exists)
        {
            throw new NotFoundException($"账户 {accountId} 未绑定到资源池 {providerGroupId}");
        }
    }

    private async Task<IReadOnlyList<ChatModelOptionOutputDto>> ResolveModelOptionsAsync(AccountToken account, CancellationToken cancellationToken)
    {
        var allCatalogModels = modelProvider.GetAllCatalogModels()
            .Where(x => !x.Value.Contains('*'))
            .ToList();

        var providerFallbackCatalogModels = modelProvider.GetAvailableModels(account.Provider)
            .Where(x => !x.Value.Contains('*'))
            .ToList();

        if (account.ModelWhites is { Count: > 0 })
        {
            return MapChatModelOptions(FilterCatalogByWhitelist(allCatalogModels, account.ModelWhites));
        }

        if (account.ModelMapping is { Count: > 0 })
        {
            return MapChatModelOptions(FilterCatalogByMapping(allCatalogModels, account.ModelMapping));
        }

        IReadOnlyList<string>? upstreamModelIds = null;
        try
        {
            await accountTokenDomainService.RefreshTokenIfNeededAsync(account, cancellationToken);
            upstreamModelIds = await accountTokenDomainService.FetchAndCacheUpstreamModelsAsync(account, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "工作区聊天模型列表拉取上游模型失败，降级静态模型: AccountId={AccountId}, Provider={Provider}", account.Id, account.Provider);
        }

        if (upstreamModelIds is { Count: > 0 })
        {
            var upstreamSet = upstreamModelIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

            return allCatalogModels
                .Where(model => IsModelExposedByUpstream(account, model.Value, upstreamSet))
                .Select(MapChatModelOption)
                .ToList();
        }

        return MapChatModelOptions(providerFallbackCatalogModels);
    }

    private async Task<bool> IsModelCurrentlyRoutableAsync(
        AccountToken account,
        string modelId,
        CancellationToken cancellationToken)
    {
        if (!await accountTokenDomainService.IsModelSupportedAsync(account, modelId, cancellationToken))
        {
            return false;
        }

        var upModelId = AccountTokenDomainService.ResolveUpModelId(
            modelId,
            account.Provider,
            account.ModelMapping,
            modelProvider);

        return account.IsModelAvailable(upModelId);
    }

    private bool IsModelExposedByUpstream(
        AccountToken account,
        string modelId,
        HashSet<string> upstreamSet)
    {
        if (modelId.Contains('*'))
        {
            return false;
        }

        var upModelId = AccountTokenDomainService.ResolveUpModelId(
            modelId,
            account.Provider,
            account.ModelMapping,
            modelProvider);

        if (string.IsNullOrWhiteSpace(upModelId))
        {
            return false;
        }

        return upstreamSet.Contains(upModelId);
    }

    private static IReadOnlyList<ModelOption> FilterCatalogByWhitelist(
        IReadOnlyList<ModelOption> catalogModels,
        IReadOnlyList<string> whitelist)
    {
        return catalogModels
            .Where(model => whitelist.Any(pattern => IsPatternMatch(model.Value, pattern)))
            .ToList();
    }

    private static IReadOnlyList<ModelOption> FilterCatalogByMapping(
        IReadOnlyList<ModelOption> catalogModels,
        Dictionary<string, string> mapping)
    {
        return catalogModels
            .Where(model => TryResolveMappingSource(model.Value, mapping) != null)
            .ToList();
    }

    private static ChatModelOptionOutputDto MapChatModelOption(ModelOption model)
    {
        return new ChatModelOptionOutputDto
        {
            Label = model.Label,
            Value = model.Value,
            Category = model.Category,
            Vendor = model.Vendor
        };
    }

    private static IReadOnlyList<ChatModelOptionOutputDto> MapChatModelOptions(IEnumerable<ModelOption> models)
    {
        return models.Select(MapChatModelOption).ToList();
    }

    private static string? TryResolveMappingSource(string modelId, Dictionary<string, string> mapping)
    {
        return AccountTokenDomainService.ResolveMapping(modelId, mapping);
    }

    private static bool IsPatternMatch(string text, string pattern)
    {
        var parts = pattern.Split('*');
        var pos = 0;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            var idx = text.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return false;
            }

            if (i == 0 && idx != 0)
            {
                return false;
            }

            pos = idx + part.Length;
        }

        return string.IsNullOrEmpty(parts[^1]) || pos == text.Length;
    }

    private Dictionary<string, int> BuildModelOrderMap()
    {
        var orderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var model in modelProvider.GetAllCatalogModels())
        {
            if (model.Value.Contains('*') || orderMap.ContainsKey(model.Value))
            {
                continue;
            }

            orderMap[model.Value] = index++;
        }

        return orderMap;
    }

    private sealed record ChatMessageCursorItem(DateTime CreationTime, Guid Id);
}

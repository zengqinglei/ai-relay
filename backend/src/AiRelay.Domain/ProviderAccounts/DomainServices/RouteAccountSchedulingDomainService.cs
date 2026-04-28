using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 路由账号调度领域服务，负责统一的选号规则计算。
/// </summary>
public class RouteAccountSchedulingDomainService(
    AccountTokenDomainService accountTokenDomainService,
    IModelProvider modelProvider,
    IDistributedCache cache,
    ILogger<RouteAccountSchedulingDomainService> logger)
{
    private const string StickySessionKeyPrefix = "sticky:session:";

    /// <summary>
    /// 在当前可参与路由的分组集合中，按统一调度规则解析本轮最佳账号。
    /// 该方法负责跨分组优先级决策；单个分组内的粘性、模型、限流、并发和权重选择
    /// 由 <see cref="ResolveBestAccountInGroupAsync"/> 负责。
    /// </summary>
    public async Task<RouteAccountSchedulingResult?> ResolveBestAccountAsync(
        IReadOnlyList<RouteAccountSchedulingGroup> groups,
        RouteAccountSchedulingContext context,
        RouteAccountSchedulingStateSnapshot stateSnapshot,
        CancellationToken cancellationToken = default)
    {
        foreach (var group in groups.OrderBy(x => x.Priority))
        {
            var result = await ResolveBestAccountInGroupAsync(group, context, stateSnapshot, cancellationToken);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// 在单个分组内解析当前最佳账号。
    /// 该方法不处理跨分组切换、最大切号数、重试编排，仅负责分组内的选号规则。
    /// </summary>
    private async Task<RouteAccountSchedulingResult?> ResolveBestAccountInGroupAsync(
        RouteAccountSchedulingGroup group,
        RouteAccountSchedulingContext context,
        RouteAccountSchedulingStateSnapshot stateSnapshot,
        CancellationToken cancellationToken)
    {
        var excludedIdSet = context.ExcludedAccountIds.ToHashSet();
        var filteredRelations = excludedIdSet.Count > 0
            ? group.CandidateRelations.Where(r => !excludedIdSet.Contains(r.AccountTokenId)).ToList()
            : group.CandidateRelations.ToList();

        if (filteredRelations.Count == 0)
        {
            logger.LogWarning("分组 '{GroupName}' 中没有符合排除条件的活跃账号", group.ProviderGroup.Name);
            return null;
        }

        // 1. 应用分组内的硬性合规过滤：
        // 账号自身可用性、模型支持能力、模型级限流状态。
        var qualifiedRelations = new List<ProviderGroupAccountRelation>();
        foreach (var relation in filteredRelations)
        {
            var account = relation.AccountToken;
            if (account == null || !account.IsAvailable())
            {
                continue;
            }

            if (!string.IsNullOrEmpty(context.RequestedModel) &&
                !await accountTokenDomainService.IsModelSupportedAsync(account, context.RequestedModel, cancellationToken))
            {
                logger.LogDebug(
                    "账号 '{Name}'({Provider}-{AuthMethod}) 不支持模型 {Model}，跳过",
                    account.Name,
                    account.Provider,
                    account.AuthMethod,
                    context.RequestedModel);
                continue;
            }

            if (!string.IsNullOrEmpty(context.RequestedModel))
            {
                var upModelId = AccountTokenDomainService.ResolveUpModelId(
                    context.RequestedModel,
                    account.Provider,
                    account.ModelMapping,
                    modelProvider);

                if (!account.IsModelAvailable(upModelId))
                {
                    logger.LogDebug(
                        "账号 '{Name}'({Provider}-{AuthMethod}) 的上游模型 {Model} 处于模型级限流，跳过",
                        account.Name,
                        account.Provider,
                        account.AuthMethod,
                        upModelId ?? context.RequestedModel);
                    continue;
                }
            }

            qualifiedRelations.Add(relation);
        }

        if (qualifiedRelations.Count == 0)
        {
            logger.LogWarning("分组 '{GroupName}' 中没有合规账号支持模型 {Model}", group.ProviderGroup.Name, context.RequestedModel);
            return null;
        }

        // 2. 粘性优先：若当前 session 已绑定到该分组下仍可用的账号，则优先复用。
        if (group.ProviderGroup.EnableStickySession && !string.IsNullOrEmpty(context.SessionHash))
        {
            var stickyAccountId = await GetStickySessionAccountAsync(group.ProviderGroup.Id, context.SessionHash, cancellationToken);
            if (stickyAccountId.HasValue)
            {
                var stickyRelation = qualifiedRelations.FirstOrDefault(r => r.AccountTokenId == stickyAccountId.Value);
                if (stickyRelation?.AccountToken != null)
                {
                    if (stateSnapshot.RateLimitedAccountIds.Contains(stickyRelation.AccountTokenId))
                    {
                        logger.LogInformation(
                            "粘性账号 '{Name}'({Provider}-{AuthMethod}) 已进入限流锁，清除粘性",
                            stickyRelation.AccountToken.Name,
                            stickyRelation.AccountToken.Provider,
                            stickyRelation.AccountToken.AuthMethod);
                        await RemoveStickySessionAsync(group.ProviderGroup.Id, context.SessionHash, cancellationToken);
                    }
                    else
                    {
                        return new RouteAccountSchedulingResult(
                            stickyRelation.AccountToken,
                            group.ProviderGroup,
                            true,
                            qualifiedRelations.Count);
                    }
                }
                else
                {
                    await RemoveStickySessionAsync(group.ProviderGroup.Id, context.SessionHash, cancellationToken);
                }
            }
        }

        // 3. 动态可用性过滤：
        // 排除当前处于限流锁中的账号，以及并发槽位已耗尽的账号。
        var readyToUseRelations = qualifiedRelations
            .Where(r => r.AccountToken != null && !stateSnapshot.RateLimitedAccountIds.Contains(r.AccountTokenId))
            .Where(r => r.AccountToken!.MaxConcurrency <= 0 ||
                        stateSnapshot.ConcurrencyCounts.GetValueOrDefault(r.AccountTokenId) < r.AccountToken.MaxConcurrency)
            .ToList();

        if (readyToUseRelations.Count == 0)
        {
            logger.LogWarning("分组 '{GroupName}' 中当前无立即可用的有效账号", group.ProviderGroup.Name);
            return null;
        }

        // 4. 动态选择：优先级优先，同优先级内按权重分配。
        var selectedRelation = SelectAccountByPriorityAndWeight(readyToUseRelations, stateSnapshot.ConcurrencyCounts);
        if (selectedRelation?.AccountToken == null)
        {
            return null;
        }

        if (group.ProviderGroup.EnableStickySession && !string.IsNullOrEmpty(context.SessionHash))
        {
            await SetStickySessionAccountAsync(
                group.ProviderGroup.Id,
                selectedRelation.AccountToken.Id,
                group.ProviderGroup.StickySessionExpirationHours,
                context.SessionHash,
                cancellationToken);
        }

        return new RouteAccountSchedulingResult(
            selectedRelation.AccountToken,
            group.ProviderGroup,
            false,
            qualifiedRelations.Count);
    }

    /// <summary>
    /// 在已通过合规过滤且具备即时可用性的账号中，执行优先级/权重选择。
    /// </summary>
    private static ProviderGroupAccountRelation? SelectAccountByPriorityAndWeight(
        List<ProviderGroupAccountRelation> relations,
        IReadOnlyDictionary<Guid, int> concurrencyCounts)
    {
        if (relations.Count == 0)
        {
            return null;
        }

        var highestPriority = relations.Min(r => r.AccountToken?.Priority ?? int.MaxValue);
        var priorityRelations = relations
            .Where(r => r.AccountToken?.Priority == highestPriority)
            .ToList();

        if (priorityRelations.Count == 1)
        {
            return priorityRelations[0];
        }

        var weightedRelations = priorityRelations
            .Select(relation =>
            {
                var account = relation.AccountToken!;
                var weight = Math.Clamp(account.Weight, 1, 100);

                if (account.MaxConcurrency > 0)
                {
                    var currentConcurrency = concurrencyCounts.GetValueOrDefault(relation.AccountTokenId);
                    var loadRate = currentConcurrency / (double)account.MaxConcurrency;
                    if (loadRate > 0.9)
                    {
                        weight = 0;
                    }
                    else if (loadRate > 0.8)
                    {
                        weight = Math.Max(1, weight / 2);
                    }
                }

                return new { Relation = relation, Weight = weight };
            })
            .Where(item => item.Weight > 0)
            .ToList();

        if (weightedRelations.Count == 0)
        {
            return priorityRelations[0];
        }

        var totalWeight = weightedRelations.Sum(item => item.Weight);
        var randomValue = Random.Shared.Next(1, totalWeight + 1);
        var currentWeight = 0;

        foreach (var item in weightedRelations)
        {
            currentWeight += item.Weight;
            if (randomValue <= currentWeight)
            {
                return item.Relation;
            }
        }

        return weightedRelations[^1].Relation;
    }

    private async Task<Guid?> GetStickySessionAccountAsync(Guid groupId, string sessionHash, CancellationToken cancellationToken)
    {
        var cacheKey = $"{StickySessionKeyPrefix}{groupId}:{sessionHash}";
        var cacheValue = await cache.GetStringAsync(cacheKey, cancellationToken);

        if (string.IsNullOrEmpty(cacheValue))
            return null;

        await cache.RefreshAsync(cacheKey, cancellationToken);

        try
        {
            var sessionData = JsonSerializer.Deserialize<StickySessionCache>(cacheValue);
            return sessionData?.AccountId;
        }
        catch
        {
            return null;
        }
    }

    private async Task SetStickySessionAccountAsync(
        Guid groupId,
        Guid accountId,
        int expirationHours,
        string sessionHash,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{StickySessionKeyPrefix}{groupId}:{sessionHash}";
        var sessionData = new StickySessionCache
        {
            AccountId = accountId,
            CreatedAt = DateTime.UtcNow
        };

        var cacheValue = JsonSerializer.Serialize(sessionData);
        var options = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(expirationHours)
        };

        await cache.SetStringAsync(cacheKey, cacheValue, options, cancellationToken);
    }

    private async Task RemoveStickySessionAsync(Guid groupId, string sessionHash, CancellationToken cancellationToken)
    {
        var cacheKey = $"{StickySessionKeyPrefix}{groupId}:{sessionHash}";
        await cache.RemoveAsync(cacheKey, cancellationToken);
    }

    private sealed class StickySessionCache
    {
        public Guid AccountId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

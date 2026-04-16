using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiRelay.Domain.ProviderGroups.DomainServices;

/// <summary>
/// 提供商分组领域服务
/// </summary>
public class ProviderGroupDomainService(
    IProviderGroupRepository providerGroupRepository,
    IProviderGroupAccountRelationRepository providerGroupAccountRelationRepository,
    AccountTokenDomainService accountTokenDomainService,
    AccountRateLimitDomainService accountRateLimitDomainService,
    IConcurrencyStrategy concurrencyStrategy,
    IDistributedCache cache,
    ILogger<ProviderGroupDomainService> logger)
{
    private const string StickySessionKeyPrefix = "sticky:session:";
    private const string DefaultGroupName = "default";

    /// <summary>
    /// 确保系统内置 default 分组存在
    /// </summary>
    public async Task EnsureDefaultProviderGroupAsync(CancellationToken cancellationToken = default)
    {
        var defaultGroup = await providerGroupRepository.GetFirstAsync(
            g => g.IsDefault || g.Name == DefaultGroupName,
            cancellationToken);

        if (defaultGroup != null)
        {
            if (!defaultGroup.IsDefault || !string.Equals(defaultGroup.Name, DefaultGroupName, StringComparison.OrdinalIgnoreCase))
            {
                defaultGroup.MarkAsDefault(DefaultGroupName);
                await providerGroupRepository.UpdateAsync(defaultGroup, cancellationToken);
            }

            return;
        }

        var group = new ProviderGroup(
            name: DefaultGroupName,
            description: "系统默认分组",
            isDefault: true,
            enableStickySession: true,
            stickySessionExpirationHours: 1,
            rateMultiplier: 1.0m);

        await providerGroupRepository.InsertAsync(group, cancellationToken);
    }

    /// <summary>
    /// 创建分组
    /// </summary>
    public async Task<ProviderGroup> CreateGroupAsync(
        string name,
        string? description,
        bool enableStickySession,
        int stickySessionExpirationHours,
        decimal rateMultiplier,
        CancellationToken cancellationToken = default)
    {
        if (await providerGroupRepository.CountAsync(g => g.Name == name, cancellationToken) > 0)
        {
            throw new BadRequestException($"已存在同名分组: {name}");
        }

        var group = new ProviderGroup(
            name: name,
            description: description,
            enableStickySession: enableStickySession,
            stickySessionExpirationHours: stickySessionExpirationHours,
            rateMultiplier: rateMultiplier);

        await providerGroupRepository.InsertAsync(group, cancellationToken);
        return group;
    }

    /// <summary>
    /// 更新分组
    /// </summary>
    public async Task<ProviderGroup> UpdateGroupAsync(
        Guid id,
        string name,
        string? description,
        bool enableStickySession,
        int stickySessionExpirationHours,
        decimal rateMultiplier,
        CancellationToken cancellationToken = default)
    {
        var group = await providerGroupRepository.GetWithDetailsAsync(id, cancellationToken)
            ?? throw new BadRequestException($"分组不存在: {id}");

        if (group.IsDefault && !string.Equals(name, DefaultGroupName, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("默认分组名称不可修改");
        }

        if (!string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase) &&
            await providerGroupRepository.CountAsync(g => g.Id != id && g.Name == name, cancellationToken) > 0)
        {
            throw new BadRequestException($"已存在同名分组: {name}");
        }

        group.Update(group.IsDefault ? DefaultGroupName : name, description, rateMultiplier);
        group.UpdateStickySession(enableStickySession, stickySessionExpirationHours);
        await providerGroupRepository.UpdateAsync(group, cancellationToken);
        return group;
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    public async Task DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await providerGroupRepository.GetWithDetailsAsync(id, cancellationToken)
            ?? throw new BadRequestException($"分组不存在: {id}");

        if (group.IsDefault)
        {
            throw new BadRequestException("默认分组不可删除");
        }

        if (group.ApiKeyBindings.Any())
        {
            throw new BadRequestException("分组已被 ApiKey 绑定，无法删除");
        }

        await providerGroupRepository.DeleteAsync(group, cancellationToken);
    }

    /// <summary>
    /// 同步账户与分组关系（全量替换）
    /// </summary>
    public async Task SyncAccountRelationsAsync(
        Guid accountId,
        IReadOnlyCollection<Guid>? providerGroupIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedGroupIds = await NormalizeProviderGroupIdsAsync(providerGroupIds, cancellationToken);
        var existingRelations = await providerGroupAccountRelationRepository.GetListAsync(r => r.AccountTokenId == accountId, cancellationToken);

        var existingGroupIdSet = existingRelations.Select(r => r.ProviderGroupId).ToHashSet();
        var targetGroupIdSet = normalizedGroupIds.ToHashSet();

        var relationsToDelete = existingRelations
            .Where(r => !targetGroupIdSet.Contains(r.ProviderGroupId))
            .ToList();

        if (relationsToDelete.Count > 0)
        {
            await providerGroupAccountRelationRepository.DeleteManyAsync(relationsToDelete, cancellationToken);
        }

        var groupIdsToAdd = normalizedGroupIds
            .Where(groupId => !existingGroupIdSet.Contains(groupId))
            .ToList();

        foreach (var groupId in groupIdsToAdd)
        {
            await providerGroupAccountRelationRepository.InsertAsync(
                new ProviderGroupAccountRelation(groupId, accountId),
                cancellationToken);
        }
    }

    /// <summary>
    /// 获取账户归属分组ID（空输入时自动回退到 default）
    /// </summary>
    public async Task<List<Guid>> NormalizeProviderGroupIdsAsync(
        IReadOnlyCollection<Guid>? providerGroupIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedIds = providerGroupIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? [];

        if (normalizedIds.Count == 0)
        {
            return [await GetDefaultProviderGroupIdAsync(cancellationToken)];
        }

        var groups = await providerGroupRepository.GetListAsync(g => normalizedIds.Contains(g.Id), cancellationToken);
        if (groups.Count() != normalizedIds.Count)
        {
            var existingIdSet = groups.Select(g => g.Id).ToHashSet();
            var missingIds = normalizedIds.Where(id => !existingIdSet.Contains(id)).ToList();
            throw new NotFoundException($"分组不存在: {string.Join(", ", missingIds)}");
        }

        return normalizedIds;
    }

    private async Task<Guid> GetDefaultProviderGroupIdAsync(CancellationToken cancellationToken)
    {
        await EnsureDefaultProviderGroupAsync(cancellationToken);

        var defaultGroup = await providerGroupRepository.GetFirstAsync(g => g.IsDefault, cancellationToken)
            ?? throw new NotFoundException("默认分组不存在");

        return defaultGroup.Id;
    }

    /// <summary>
    /// 为 ApiKey 选择账户（返回账户和分组）
    /// </summary>
    /// <param name="group">资源池分组实例</param>
    /// <param name="apiKeyId">API密钥ID</param>
    /// <param name="apiKeyName">API密钥名称</param>
    /// <param name="sessionHash">会话哈希</param>
    /// <param name="excludedIds">排除的账号ID列表（用于重试时排除已失败的账号）</param>
    /// <param name="requestedModel">可选：请求的模型</param>
    /// <param name="allowedCombinations">可选：当前路由端点允许的 (Provider, AuthMethod) 组合，为 null 时跳过此过滤</param>
    /// <returns>选中的账户、分组信息、是否为粘性绑定、可用账号总数</returns>
    public async Task<(AccountToken? AccountToken, ProviderGroup Group, bool IsStickyBound, int AvailableCount)?> SelectAccountForApiKeyAsync(
        ProviderGroup group,
        Guid apiKeyId,
        string? apiKeyName,
        string? sessionHash = null,
        IEnumerable<Guid>? excludedIds = null,
        string? requestedModel = null,
        IReadOnlyList<(Provider Provider, AuthMethod AuthMethod)>? allowedCombinations = null)
    {
        Guid groupId = group.Id;
        var excludedIdList = excludedIds?.ToList() ?? [];

        logger.LogDebug("ApiKey '{ApiKeyName}' 使用分组 '{GroupName}'", apiKeyName ?? "未知", group.Name);

        // 1. SQL 下压过滤：获取符合协议、排除列表且活跃的原始账号列表
        var relations = await providerGroupAccountRelationRepository.GetCandidatesAsync(
            groupId,
            allowedCombinations?.ToList(),
            excludedIdList);

        if (!relations.Any())
        {
            logger.LogWarning("分组 '{GroupName}' 中没有符合协议或排除条件的活跃账号", group.Name);
            return (null, group, false, 0);
        }

        // 2. 批量获取动态状态量（提高并发检查与限流检查效率）
        var accountIds = relations.Select(r => r.AccountTokenId).Distinct().ToList();
        var concurrencyTask = concurrencyStrategy.GetConcurrencyCountsAsync(accountIds);
        var rateLimitTask = accountRateLimitDomainService.GetRateLimitedAccountIdsAsync(accountIds);
        await Task.WhenAll(concurrencyTask, rateLimitTask);

        var concurrencyCounts = concurrencyTask.Result;
        var rateLimitedIds = rateLimitTask.Result;

        // 3. 应用层流水线过滤：提取符合“硬性合规性”的候选人列表 (Qualified Candidates)
        var qualifiedRelations = new List<ProviderGroupAccountRelation>();
        foreach (var relation in relations)
        {
            var account = relation.AccountToken;
            if (account == null)
            {
                continue;
            }

            if (!account.IsAvailable())
            {
                continue;
            }

            if (!string.IsNullOrEmpty(requestedModel) && !await accountTokenDomainService.IsModelSupportedAsync(account, requestedModel))
            {
                logger.LogDebug(
                    "账号 '{Name}'({Provider}-{AuthMethod}) 不支持模型 {Model}，跳过",
                    account.Name,
                    account.Provider,
                    account.AuthMethod,
                    requestedModel);
                continue;
            }

            qualifiedRelations.Add(relation);
        }

        if (qualifiedRelations.Count == 0)
        {
            logger.LogWarning("分组 '{GroupName}' 中没有合规账号支持模型 {Model}", group.Name, requestedModel);
            return (null, group, false, 0);
        }

        // 4.1 粘性优先
        if (group.EnableStickySession && !string.IsNullOrEmpty(sessionHash))
        {
            var stickyAccountId = await GetStickySessionAccountAsync(groupId, sessionHash);
            if (stickyAccountId.HasValue)
            {
                var stickyRelation = qualifiedRelations.FirstOrDefault(r => r.AccountTokenId == stickyAccountId.Value);
                if (stickyRelation?.AccountToken != null)
                {
                    if (rateLimitedIds.Contains(stickyRelation.AccountTokenId))
                    {
                        logger.LogInformation(
                            "粘性账号 '{Name}'({Provider}-{AuthMethod}) 已进入限流锁，清除粘性",
                            stickyRelation.AccountToken.Name,
                            stickyRelation.AccountToken.Provider,
                            stickyRelation.AccountToken.AuthMethod);
                        await RemoveStickySessionAsync(groupId, sessionHash);
                    }
                    else
                    {
                        return (stickyRelation.AccountToken, group, true, qualifiedRelations.Count);
                    }
                }
                else
                {
                    await RemoveStickySessionAsync(groupId, sessionHash);
                }
            }
        }

        // 4.2 动态选择：优先级优先，同优先级内按权重分配
        var readyToUseRelations = qualifiedRelations
            .Where(r => r.AccountToken != null && !rateLimitedIds.Contains(r.AccountTokenId))
            .Where(r => r.AccountToken!.MaxConcurrency <= 0 ||
                        concurrencyCounts.GetValueOrDefault(r.AccountTokenId) < r.AccountToken.MaxConcurrency)
            .ToList();

        if (readyToUseRelations.Count > 0)
        {
            var selectedRelation = SelectAccountByPriorityAndWeight(readyToUseRelations, concurrencyCounts);
            if (selectedRelation?.AccountToken != null)
            {
                if (group.EnableStickySession && !string.IsNullOrEmpty(sessionHash))
                {
                    await SetStickySessionAccountAsync(
                        groupId,
                        selectedRelation.AccountToken.Id,
                        group.StickySessionExpirationHours,
                        sessionHash);
                }

                return (selectedRelation.AccountToken, group, false, qualifiedRelations.Count);
            }
        }

        logger.LogWarning("分组 '{GroupName}' 中当前无立即（可用状态）的有效账号", group.Name);
        return (null, group, false, qualifiedRelations.Count);
    }

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

    /// <summary>
    /// 获取粘性会话中的账户ID
    /// </summary>
    public async Task<Guid?> GetStickySessionAccountAsync(Guid groupId, string sessionHash)
    {
        var cacheKey = $"{StickySessionKeyPrefix}{groupId}:{sessionHash}";
        var cacheValue = await cache.GetStringAsync(cacheKey);

        if (string.IsNullOrEmpty(cacheValue))
            return null;

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

    /// <summary>
    /// 设置粘性会话
    /// </summary>
    private async Task SetStickySessionAccountAsync(
        Guid groupId,
        Guid accountId,
        int expirationHours,
        string sessionHash)
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
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(expirationHours)
        };

        await cache.SetStringAsync(cacheKey, cacheValue, options);
    }

    /// <summary>
    /// 移除粘性会话
    /// </summary>
    private async Task RemoveStickySessionAsync(Guid groupId, string sessionHash)
    {
        var cacheKey = $"{StickySessionKeyPrefix}{groupId}:{sessionHash}";
        await cache.RemoveAsync(cacheKey);
    }

    private class StickySessionCache
    {
        public Guid AccountId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

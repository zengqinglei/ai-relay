using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using AiRelay.Domain.ProviderGroups.ValueObjects;
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
    IRepository<ProviderGroup, Guid> providerGroupRepository,
    IProviderGroupAccountRelationRepository providerGroupAccountRelationRepository,
    IRepository<AccountToken, Guid> accountTokenRepository,
    IRepository<ApiKeyProviderGroupBinding, Guid> apiKeyProviderGroupBindingRepository,
    AccountTokenDomainService accountTokenDomainService,
    GroupSchedulingStrategyFactory groupSchedulingStrategyFactory,
    AccountRateLimitDomainService accountRateLimitDomainService,
    IConcurrencyStrategy concurrencyStrategy,
    IDistributedCache cache,
    ILogger<ProviderGroupDomainService> logger)
{
    private const string StickySessionKeyPrefix = "sticky:session:";

    /// <summary>
    /// 创建分组并关联账户
    /// </summary>
    public async Task<ProviderGroup> CreateGroupWithAccountsAsync(
        string name,
        string? description,
        ProviderPlatform platform,
        GroupSchedulingStrategy schedulingStrategy,
        bool enableStickySession,
        int stickySessionExpirationHours,
        decimal rateMultiplier,
        List<(Guid AccountId, int Priority, int Weight)> accounts,
        CancellationToken cancellationToken = default)
    {
        // 1. 唯一性校验
        if (await providerGroupRepository.CountAsync(g => g.Name == name && g.Platform == platform, cancellationToken) > 0)
        {
            throw new BadRequestException($"平台 {platform} 下已存在同名分组: {name}");
        }

        // 2. 创建分组
        var group = new ProviderGroup(
            name,
            description,
            platform,
            schedulingStrategy,
            enableStickySession,
            stickySessionExpirationHours,
            rateMultiplier);

        await providerGroupRepository.InsertAsync(group, cancellationToken);

        // 3. 处理账户关联
        if (accounts.Count != 0)
        {
            await ProcessAccountRelationsAsync(group, accounts, cancellationToken);
        }

        return group;
    }

    /// <summary>
    /// 更新分组及账户关联
    /// </summary>
    public async Task<ProviderGroup> UpdateGroupWithAccountsAsync(
        Guid id,
        string name,
        string? description,
        GroupSchedulingStrategy schedulingStrategy,
        bool enableStickySession,
        int stickySessionExpirationHours,
        decimal rateMultiplier,
        List<(Guid AccountId, int Priority, int Weight)> accounts,
        CancellationToken cancellationToken = default)
    {
        var group = await providerGroupRepository.GetByIdAsync(id, cancellationToken);
        if (group == null) throw new BadRequestException($"分组不存在: {id}");

        // 1. 唯一性校验
        if (group.Name != name)
        {
            if (await providerGroupRepository.CountAsync(g => g.Id != id && g.Name == name && g.Platform == group.Platform, cancellationToken) > 0)
            {
                throw new BadRequestException($"平台 {group.Platform} 下已存在同名分组: {name}");
            }
        }

        // 2. 更新分组
        group.Update(name, description, schedulingStrategy, rateMultiplier);
        group.UpdateStickySession(enableStickySession, stickySessionExpirationHours);
        await providerGroupRepository.UpdateAsync(group, cancellationToken);

        // 3. 更新账户关联 (全量替换)
        // 3.1 删除旧关联
        var existingRelations = await providerGroupAccountRelationRepository.GetListAsync(r => r.ProviderGroupId == id, cancellationToken);
        if (existingRelations.Any())
        {
            await providerGroupAccountRelationRepository.DeleteManyAsync(existingRelations, cancellationToken);
        }

        // 3.2 添加新关联
        if (accounts.Any())
        {
            await ProcessAccountRelationsAsync(group, accounts, cancellationToken);
        }

        return group;
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    public async Task DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await providerGroupRepository.GetByIdAsync(id, cancellationToken);
        if (group == null) throw new BadRequestException($"分组不存在: {id}");

        // 检查是否有 ApiKey 绑定
        var bindings = await apiKeyProviderGroupBindingRepository.GetListAsync(b => b.ProviderGroupId == id, cancellationToken);
        if (bindings.Any())
        {
            throw new BadRequestException("分组已被 ApiKey 绑定，无法删除");
        }

        // 级联软删除分组账户关系
        var relations = await providerGroupAccountRelationRepository.GetListAsync(r => r.ProviderGroupId == id, cancellationToken);
        if (relations.Any())
        {
            await providerGroupAccountRelationRepository.DeleteManyAsync(relations, cancellationToken);
        }

        await providerGroupRepository.DeleteAsync(group, cancellationToken);
    }

    /// <summary>
    /// 处理账户关联（校验并创建）
    /// </summary>
    private async Task ProcessAccountRelationsAsync(
        ProviderGroup group,
        List<(Guid AccountId, int Priority, int Weight)> accounts,
        CancellationToken cancellationToken)
    {
        var accountIds = accounts.Select(a => a.AccountId).Distinct().ToList();
        var accountTokens = await accountTokenRepository.GetListAsync(a => accountIds.Contains(a.Id), cancellationToken);
        var accountTokenMap = accountTokens.ToDictionary(a => a.Id);

        var relationsToInsert = new List<ProviderGroupAccountRelation>();

        foreach (var (accountId, priority, weight) in accounts)
        {
            if (!accountTokenMap.TryGetValue(accountId, out var accountToken))
            {
                throw new NotFoundException($"账户不存在: {accountId}");
            }

            if (accountToken.Platform != group.Platform)
            {
                throw new BadRequestException(
                    $"账户平台不匹配: {accountId} 是 {accountToken.Platform}，分组要求 {group.Platform}");
            }

            var relation = new ProviderGroupAccountRelation(
                group.Id,
                accountId,
                priority,
                weight);

            relationsToInsert.Add(relation);
        }

        if (relationsToInsert.Any())
        {
            await providerGroupAccountRelationRepository.InsertManyAsync(relationsToInsert, cancellationToken);
        }
    }

    /// <summary>
    /// 为 ApiKey 选择账户（返回账户和分组）
    /// </summary>
    /// <param name="groupId">分组ID</param>
    /// <param name="apiKeyId">API密钥ID</param>
    /// <param name="apiKeyName">API密钥名称</param>
    /// <param name="platform">平台</param>
    /// <param name="sessionHash">会话哈希</param>
    /// <param name="excludedIds">排除的账号ID列表（用于重试时排除已失败的账号）</param>
    /// <returns>选中的账户、分组信息、是否为粘性绑定、可用账号总数</returns>
    public async Task<(AccountToken? AccountToken, ProviderGroup Group, bool IsStickyBound, int AvailableCount)?> SelectAccountForApiKeyAsync(
        Guid groupId,
        Guid apiKeyId,
        string? apiKeyName,
        ProviderPlatform platform,
        string? sessionHash = null,
        IEnumerable<Guid>? excludedIds = null,
        string? requestedModel = null)
    {
        // 1. 获取分组信息
        var group = await providerGroupRepository.GetByIdAsync(groupId);
        if (group == null)
            throw new NotFoundException($"分组不存在: {groupId}");

        logger.LogDebug("ApiKey '{ApiKeyName}' 使用分组 '{GroupName}'", apiKeyName ?? "未知", group.Name);

        // 2. 检查粘性会话
        // 只有提供了 sessionHash 且分组启用了粘性会话时才生效
        if (group.EnableStickySession && !string.IsNullOrEmpty(sessionHash))
        {
            var stickyAccountId = await GetStickySessionAccountAsync(groupId, platform, sessionHash);
            if (stickyAccountId.HasValue)
            {
                // 排除列表检查
                if (excludedIds != null && excludedIds.Contains(stickyAccountId.Value))
                {
                    logger.LogInformation("粘性会话账户 {AccountId} 在排除列表中，跳过并清除粘性会话", stickyAccountId.Value);
                    await RemoveStickySessionAsync(groupId, platform, sessionHash);
                }
                else
                {
                    // 检查该账户是否在当前分组的关联关系中，且状态正常
                    // Cache First 策略：如果缓存的账户不可用，立即失效并重新调度
                    var relation = await providerGroupAccountRelationRepository.GetFirstAsync(r =>
                        r.ProviderGroupId == groupId &&
                        r.AccountTokenId == stickyAccountId.Value &&
                        r.IsActive);

                    if (relation != null)
                    {
                        var stickyAccount = await accountTokenRepository.GetByIdAsync(stickyAccountId.Value);
                        // 添加限流检查
                        var isRateLimited = await accountRateLimitDomainService.IsRateLimitedAsync(stickyAccountId.Value);
                        if (stickyAccount != null && stickyAccount.IsActive && stickyAccount.IsAvailable() && !isRateLimited)
                        {
                            return (stickyAccount, group, true, 1); // 粘性绑定，可用数量设为1（因为粘性会话只用这个账号）
                        }
                    }

                    // 缓存未命中或账户不可用：清除缓存，进入重新调度
                    await RemoveStickySessionAsync(groupId, platform, sessionHash);
                }
            }
        }

        // 3. 使用自定义仓储方法获取有效的分组关联及账户 (包含 AccountToken)
        // 这个方法已经在仓储层封装了 Include(AccountToken) 和 Where(IsActive) 逻辑
        var relations = await providerGroupAccountRelationRepository.GetListByGroupIdWithAccountsAsync(groupId);

        if (!relations.Any())
            return (null, group, false, 0);

        // 4. 批量获取并发数和限流状态（提前获取，避免在循环中多次查询）
        var candidateIds = relations.Select(r => r.AccountTokenId).Distinct().ToList();
        var concurrencyTask = concurrencyStrategy.GetConcurrencyCountsAsync(candidateIds);
        var rateLimitTask = accountRateLimitDomainService.GetRateLimitedAccountIdsAsync(candidateIds);

        await Task.WhenAll(concurrencyTask, rateLimitTask);

        var concurrencyCounts = concurrencyTask.Result;
        var rateLimitedIds = rateLimitTask.Result;

        // 5. 一次性过滤：排除列表 + 可用性 + 限流 + 并发控制
        var excludedIdSet = excludedIds?.ToHashSet() ?? [];
        var availableRelations = new List<ProviderGroupAccountRelation>();

        foreach (var relation in relations)
        {
            // 过滤1：排除列表
            if (excludedIdSet.Contains(relation.AccountTokenId))
                continue;

            // 过滤2：可用性检查
            if (relation.AccountToken == null || !relation.AccountToken.IsAvailable())
                continue;

            // 过滤3：限流检查（批量查询结果）
            if (rateLimitedIds.Contains(relation.AccountTokenId))
            {
                logger.LogDebug("账号 {AccountId} 被限流跳过", relation.AccountTokenId);
                continue;
            }

            // 过滤4：并发控制
            var account = relation.AccountToken;
            var currentConcurrency = concurrencyCounts.TryGetValue(relation.AccountTokenId, out var count) ? count : 0;

            if (account.MaxConcurrency > 0 && currentConcurrency >= account.MaxConcurrency)
            {
                logger.LogDebug("账号 {AccountId} 已满载 ({Current}/{Max})，跳过",
                    relation.AccountTokenId, currentConcurrency, account.MaxConcurrency);
                continue;
            }

            // 过滤5：模型支持检查
            if (!string.IsNullOrEmpty(requestedModel))
            {
                var isSupported = await accountTokenDomainService.IsModelSupportedAsync(account, requestedModel);
                if (!isSupported)
                {
                    logger.LogDebug("账号 {AccountId} 不支持模型 {Model}，跳过", relation.AccountTokenId, requestedModel);
                    continue;
                }
            }

            availableRelations.Add(relation);
        }

        // 如果所有账号都不可用（被排除、限流、满载等），返回 null
        if (availableRelations.Count == 0)
        {
            logger.LogWarning("分组 {GroupId} 中没有可用账号（总数: {Total}）", groupId, relations.Count);
            return (null, group, false, 0);
        }

        logger.LogDebug("从 {Count} 个可用账户中选择", availableRelations.Count);

        // 6. 使用调度策略选择账户
        var strategy = groupSchedulingStrategyFactory.CreateStrategy(group.SchedulingStrategy);
        var selectedRelation = await strategy.SelectAccountAsync(
            availableRelations,
            concurrencyCounts);

        if (selectedRelation == null)
            return (null, group, false, availableRelations.Count);

        // 从选中的关系中直接获取已填充的账户对象
        var selectedAccount = selectedRelation.AccountToken;

        // 7. 设置粘性会话（选中的账号一定是非满载的，可以安全设置）
        if (group.EnableStickySession && !string.IsNullOrEmpty(sessionHash))
        {
            await SetStickySessionAccountAsync(
                groupId,
                platform,
                selectedAccount!.Id,
                group.StickySessionExpirationHours,
                sessionHash);
        }

        return (selectedAccount, group, false, availableRelations.Count); // 非粘性绑定（新选出的账号）
    }

    /// <summary>
    /// 获取粘性会话中的账户ID
    /// </summary>
    public async Task<Guid?> GetStickySessionAccountAsync(Guid groupId, ProviderPlatform platform, string sessionHash)
    {
        var cacheKey = $"{StickySessionKeyPrefix}{groupId}:{platform}:{sessionHash}";
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
        ProviderPlatform platform,
        Guid accountId,
        int expirationHours,
        string sessionHash)
    {
        var cacheKey = $"{StickySessionKeyPrefix}{groupId}:{platform}:{sessionHash}";
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
    private async Task RemoveStickySessionAsync(Guid groupId, ProviderPlatform platform, string sessionHash)
    {
        var cacheKey = $"{StickySessionKeyPrefix}{groupId}:{platform}:{sessionHash}";
        await cache.RemoveAsync(cacheKey);
    }

    private class StickySessionCache
    {
        public Guid AccountId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

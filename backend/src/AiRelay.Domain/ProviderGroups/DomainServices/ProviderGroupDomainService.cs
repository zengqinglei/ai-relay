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
    IProviderGroupRepository providerGroupRepository,
    IProviderGroupAccountRelationRepository providerGroupAccountRelationRepository,
    IRepository<AccountToken, Guid> accountTokenRepository,
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
        GroupSchedulingStrategy schedulingStrategy,
        bool enableStickySession,
        int stickySessionExpirationHours,
        decimal rateMultiplier,
        List<(Guid AccountId, int Priority, int Weight)> accounts,
        CancellationToken cancellationToken = default)
    {
        // 1. 唯一性校验
        if (await providerGroupRepository.CountAsync(g => g.Name == name, cancellationToken) > 0)
        {
            throw new BadRequestException($"已存在同名分组: {name}");
        }

        // 2. 创建分组
        var group = new ProviderGroup(
            name,
            description,
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
        var group = await providerGroupRepository.GetWithDetailsAsync(id, cancellationToken);
        if (group == null) throw new BadRequestException($"分组不存在: {id}");

        // 1. 唯一性校验
        if (group.Name != name)
        {
            if (await providerGroupRepository.CountAsync(g => g.Id != id && g.Name == name, cancellationToken) > 0)
            {
                throw new BadRequestException($"已存在同名分组: {name}");
            }
        }

        // 2. 更新分组主体，先单独保存避免与关联记录混入同一 SaveChanges 批次
        group.Update(name, description, schedulingStrategy, rateMultiplier);
        group.UpdateStickySession(enableStickySession, stickySessionExpirationHours);
        await providerGroupRepository.UpdateAsync(group, cancellationToken);

        // 3. 更新账户关联（全量替换，显式操作独立 SaveChanges）
        var existing = group.Relations.ToList();
        if (existing.Any())
        {
            await providerGroupAccountRelationRepository.DeleteManyAsync(existing, cancellationToken);
            group.Relations.Clear();
        }

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
        // 使用 GetWithDetailsAsync 确保加载了 Relations，从而触发 EF Core 的级联删除逻辑
        var group = await providerGroupRepository.GetWithDetailsAsync(id, cancellationToken);
        if (group == null) throw new BadRequestException($"分组不存在: {id}");

        // 检查是否有 ApiKey 绑定 (利用预加载的集合)
        if (group.ApiKeyBindings.Any())
        {
            throw new BadRequestException("分组已被 ApiKey 绑定，无法删除");
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

            var relation = new ProviderGroupAccountRelation(
                group.Id,
                accountId,
                priority,
                weight);

            relationsToInsert.Add(relation);
        }

        foreach (var relation in relationsToInsert)
        {
            group.Relations.Add(relation);
            await providerGroupAccountRelationRepository.InsertAsync(relation, cancellationToken);
        }
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
            logger.LogWarning("分组 {GroupId} 中没有符合协议或排除条件的活跃账号", groupId);
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
        // 包含：基础状态检查、复杂的模型支持性检查。
        var qualifiedRelations = new List<ProviderGroupAccountRelation>();
        foreach (var relation in relations)
        {
            var account = relation.AccountToken!;
            
            // 基础可用性检查 (Status != Error)
            if (!account.IsAvailable()) continue;

            // 模型支持检查 (包含白名单与通配符逻辑)
            if (!string.IsNullOrEmpty(requestedModel))
            {
                if (!await accountTokenDomainService.IsModelSupportedAsync(account, requestedModel))
                {
                    logger.LogDebug("账号 {AccountName} 不支持模型 {Model}，跳过", account.Name, requestedModel);
                    continue;
                }
            }

            qualifiedRelations.Add(relation);
        }

        if (qualifiedRelations.Count == 0)
        {
            logger.LogWarning("分组 {GroupId} 中没有合规账号支持模型 {Model}", groupId, requestedModel);
            return (null, group, false, 0);
        }

        // 4. 决策分流：
        
        // 4.1 分支 A: 粘性优先
        if (group.EnableStickySession && !string.IsNullOrEmpty(sessionHash))
        {
            var stickyAccountId = await GetStickySessionAccountAsync(groupId, sessionHash);
            if (stickyAccountId.HasValue)
            {
                var stickyRel = qualifiedRelations.FirstOrDefault(r => r.AccountTokenId == stickyAccountId.Value);
                if (stickyRel != null)
                {
                    // 检查状态：如果被熔断则清除粘性；若仅是并发满载，则允许返回（由中间件处理等待）
                    if (rateLimitedIds.Contains(stickyRel.AccountTokenId))
                    {
                        logger.LogInformation("粘性账号 {AccountName} 已进入限流锁，清除粘性", stickyRel.AccountToken.Name);
                        await RemoveStickySessionAsync(groupId, sessionHash);
                    }
                    else
                    {
                        return (stickyRel.AccountToken, group, true, qualifiedRelations.Count);
                    }
                }
                else
                {
                    // 已粘性的账号不符合当前请求要求（如模型/协议不一致），清除粘性介入重调
                    await RemoveStickySessionAsync(groupId, sessionHash);
                }
            }
        }

        // 4.2 分支 B: 动态调度选择
        // 过滤掉当前不可调用的账号（限流中、并发已满）
        var readyToUseRelations = qualifiedRelations
            .Where(r => !rateLimitedIds.Contains(r.AccountTokenId))
            .Where(r => r.AccountToken.MaxConcurrency <= 0 || 
                       concurrencyCounts.GetValueOrDefault(r.AccountTokenId) < r.AccountToken.MaxConcurrency)
            .ToList();

        if (readyToUseRelations.Count > 0)
        {
            var strategy = groupSchedulingStrategyFactory.CreateStrategy(group.SchedulingStrategy);
            var selectedRelation = await strategy.SelectAccountAsync(readyToUseRelations, concurrencyCounts);

            if (selectedRelation != null)
            {
                var selectedAccount = selectedRelation.AccountToken;
                
                // 设置粘性会话
                if (group.EnableStickySession && !string.IsNullOrEmpty(sessionHash))
                {
                    await SetStickySessionAccountAsync(groupId, selectedAccount.Id, group.StickySessionExpirationHours, sessionHash);
                }

                return (selectedAccount, group, false, qualifiedRelations.Count);
            }
        }

        // 如果没有立即可以使用的账号，返回 null，上层将根据是否重试进行后续处理
        logger.LogWarning("分组 {GroupId} 中当前无立立即（可用状态）的有效账号", groupId);
        return (null, group, false, qualifiedRelations.Count);
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

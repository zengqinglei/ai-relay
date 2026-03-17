using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;

/// <summary>
/// 配额优先调度策略
/// </summary>
public class QuotaPriorityStrategy(
    IDistributedCache cache,
    AccountRateLimitDomainService accountRateLimitDomainService,
    ILogger<QuotaPriorityStrategy> logger) : IGroupSchedulingStrategy
{
    public async Task<ProviderGroupAccountRelation?> SelectAccountAsync(
        IReadOnlyList<ProviderGroupAccountRelation> relations,
        Func<IEnumerable<Guid>, Task<Dictionary<Guid, long>>> usageProvider,
        IReadOnlyDictionary<Guid, int> concurrencyCounts)
    {
        if (relations.Count == 0)
            return null;

        // 并行过滤掉处于限流状态的账户
        var availableRelations = new ConcurrentBag<ProviderGroupAccountRelation>();
        await Task.WhenAll(relations.Select(async r =>
        {
            if (!await accountRateLimitDomainService.IsRateLimitedAsync(r.AccountTokenId))
            {
                availableRelations.Add(r);
            }
        }));

        if (availableRelations.IsEmpty)
        {
            logger.LogWarning("所有账户均处于限流状态");
            return null;
        }

        // 并行获取所有可用账户的配额信息
        var quotaMap = new ConcurrentDictionary<Guid, int>();

        var tasks = availableRelations.Select(async relation =>
        {
            var cacheKey = $"account:quota:{relation.AccountTokenId}";
            try
            {
                var cachedData = await cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    var quotaInfo = JsonSerializer.Deserialize<AccountQuotaInfo>(cachedData);
                    if (quotaInfo?.RemainingQuota.HasValue == true)
                    {
                        quotaMap[relation.AccountTokenId] = quotaInfo.RemainingQuota.Value;
                        logger.LogDebug(
                            "账户 {AccountId} 配额: {Quota}",
                            relation.AccountTokenId,
                            quotaInfo.RemainingQuota.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "获取/解析账户 {AccountId} 配额信息失败", relation.AccountTokenId);
            }
        });

        await Task.WhenAll(tasks);

        // 按配额降序、优先级升序排序
        // 二级排序增加：当前并发数升序（优先选空闲的）
        var selectedRelation = availableRelations
            .Select(r => new
            {
                Relation = r,
                Quota = quotaMap.GetValueOrDefault(r.AccountTokenId, 0),
                Current = concurrencyCounts.GetValueOrDefault(r.AccountTokenId, 0)
            })
            .OrderByDescending(x => x.Quota) // 配额降序（配额越多越优先）
            .ThenBy(x => x.Relation.Priority) // 优先级升序（数值越小越优先）
            .ThenBy(x => x.Current) // 并发数升序
            .FirstOrDefault();

        if (selectedRelation != null)
        {
            logger.LogInformation(
                "QuotaPriority 策略选中账户 {AccountId}, 配额: {Quota}, 优先级: {Priority}, 当前并发: {Concurrency}",
                selectedRelation.Relation.AccountTokenId,
                selectedRelation.Quota,
                selectedRelation.Relation.Priority,
                selectedRelation.Current);
        }

        return selectedRelation?.Relation;
    }
}

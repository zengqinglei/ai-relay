using AiRelay.Domain.ProviderGroups.Entities;

namespace AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;

/// <summary>
/// 自适应均衡调度策略 (基于剩余容量加权的最少使用策略)
/// </summary>
public class AdaptiveBalancedStrategy : IGroupSchedulingStrategy
{
    public async Task<ProviderGroupAccountRelation?> SelectAccountAsync(
        IReadOnlyList<ProviderGroupAccountRelation> relations,
        Func<IEnumerable<Guid>, Task<Dictionary<Guid, long>>> usageProvider,
        IReadOnlyDictionary<Guid, int> concurrencyCounts)
    {
        if (relations.Count == 0)
            return null;

        // 1. 批量获取每日用量 (IO 操作)
        var accountIds = relations.Select(r => r.AccountTokenId).Distinct().ToList();
        var dailyUsages = await usageProvider(accountIds);

        // 2. 计算得分并选择最低分
        // Score = (DailyUsage + 1) / (RemainingConcurrency + 1)
        // 得分越低越好
        var bestOption = relations
            .Select(r =>
            {
                var usage = dailyUsages.GetValueOrDefault(r.AccountTokenId, 0);
                var currentConcurrency = concurrencyCounts.GetValueOrDefault(r.AccountTokenId, 0);

                // 获取最大并发 (如果未设置，默认给一个较大值，如 100，避免除以 0 或逻辑错误)
                // 注意：这里依赖 r.AccountToken 导航属性已被加载
                var maxConcurrency = r.AccountToken?.MaxConcurrency ?? 100;

                // 计算剩余槽位 (不小于 0)
                var availableSlots = Math.Max(0, maxConcurrency - currentConcurrency);

                // 计算得分 (使用 double 避免整除精度丢失)
                double score = (double)(usage + 1) / (availableSlots + 1);

                return new { Relation = r, Score = score };
            })
            .OrderBy(x => x.Score) // 分数越低越优先
            .ThenBy(x => dailyUsages.GetValueOrDefault(x.Relation.AccountTokenId, 0))
            .ThenBy(x => x.Relation.Priority) // 分数相同按最少请求次数
            .FirstOrDefault();

        return bestOption?.Relation;
    }
}

using AiRelay.Domain.ProviderGroups.Entities;

namespace AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;

/// <summary>
/// 自适应均衡调度策略 (基于剩余容量加权的最少使用策略)
/// </summary>
public class AdaptiveBalancedStrategy : IGroupSchedulingStrategy
{
    public Task<ProviderGroupAccountRelation?> SelectAccountAsync(
        IReadOnlyList<ProviderGroupAccountRelation> relations,
        IReadOnlyDictionary<Guid, int> concurrencyCounts)
    {
        if (relations.Count == 0)
            return Task.FromResult<ProviderGroupAccountRelation?>(null);

        // Score = (UsageToday + 1) / (RemainingConcurrency + 1)，得分越低越好
        var bestOption = relations
            .Select(r =>
            {
                var usage = r.AccountToken?.UsageToday ?? 0;
                var currentConcurrency = concurrencyCounts.GetValueOrDefault(r.AccountTokenId, 0);
                var maxConcurrency = r.AccountToken?.MaxConcurrency ?? 100;
                var availableSlots = Math.Max(0, maxConcurrency - currentConcurrency);
                double score = (double)(usage + 1) / (availableSlots + 1);
                return new { Relation = r, Score = score, Usage = usage };
            })
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Usage)
            .ThenBy(x => x.Relation.Priority)
            .FirstOrDefault();

        return Task.FromResult(bestOption?.Relation);
    }
}

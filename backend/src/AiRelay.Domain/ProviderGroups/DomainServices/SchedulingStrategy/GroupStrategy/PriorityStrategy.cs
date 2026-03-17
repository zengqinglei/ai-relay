using AiRelay.Domain.ProviderGroups.Entities;

namespace AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;

/// <summary>
/// 优先级降级调度策略
/// </summary>
public class PriorityStrategy : IGroupSchedulingStrategy
{
    public Task<ProviderGroupAccountRelation?> SelectAccountAsync(
        IReadOnlyList<ProviderGroupAccountRelation> relations,
        Func<IEnumerable<Guid>, Task<Dictionary<Guid, long>>> usageProvider,
        IReadOnlyDictionary<Guid, int> concurrencyCounts)
    {
        if (relations.Count == 0)
            return Task.FromResult<ProviderGroupAccountRelation?>(null);

        // 按优先级排序（值越小优先级越高）
        // 如果优先级相同，选择当前并发负载率最低的
        var selectedRelation = relations
            .Select(r => new
            {
                Relation = r,
                Current = concurrencyCounts.GetValueOrDefault(r.AccountTokenId, 0),
                Max = r.AccountToken?.MaxConcurrency ?? int.MaxValue
            })
            .OrderBy(x => x.Relation.Priority)
            .ThenBy(x => (double)x.Current / (x.Max == 0 ? 1 : x.Max)) // 负载率升序
            .FirstOrDefault();

        return Task.FromResult(selectedRelation?.Relation);
    }
}

using AiRelay.Domain.ProviderGroups.Entities;

namespace AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;

/// <summary>
/// 加权随机调度策略
/// </summary>
public class WeightedRandomStrategy : IGroupSchedulingStrategy
{
    private readonly Random _random = new();

    public Task<ProviderGroupAccountRelation?> SelectAccountAsync(
        IReadOnlyList<ProviderGroupAccountRelation> relations,
        Func<IEnumerable<Guid>, Task<Dictionary<Guid, long>>> usageProvider,
        IReadOnlyDictionary<Guid, int> concurrencyCounts)
    {
        if (relations.Count == 0)
            return Task.FromResult<ProviderGroupAccountRelation?>(null);

        // 计算动态权重
        var dynamicRelations = relations.Select(r =>
        {
            var current = concurrencyCounts.GetValueOrDefault(r.AccountTokenId, 0);
            var max = r.AccountToken?.MaxConcurrency ?? int.MaxValue;
            var loadRate = (double)current / (max == 0 ? 1 : max);

            int tempWeight = r.Weight;
            if (loadRate > 0.9) tempWeight = 0; // 极高负载暂时剔除
            else if (loadRate > 0.8) tempWeight = Math.Max(1, r.Weight / 2); // 高负载权重减半

            return new { Relation = r, Weight = tempWeight };
        }).Where(x => x.Weight > 0).ToList();

        if (dynamicRelations.Count == 0)
        {
            // 如果所有都满载，回退到使用原始权重 (或返回空由上层处理排队)
            // 这里选择回退，因为上层可能已经过滤过满载了，但如果所有都 > 90% 负载，还是要选一个
            return Task.FromResult<ProviderGroupAccountRelation?>(relations[0]);
        }

        // 计算总权重
        var totalWeight = dynamicRelations.Sum(r => r.Weight);

        // 生成随机数
        var randomValue = _random.Next(1, totalWeight + 1);

        // 根据权重选择账户
        var currentWeight = 0;
        foreach (var item in dynamicRelations)
        {
            currentWeight += item.Weight;
            if (randomValue <= currentWeight)
                return Task.FromResult<ProviderGroupAccountRelation?>(item.Relation);
        }

        return Task.FromResult<ProviderGroupAccountRelation?>(dynamicRelations[0].Relation);
    }
}

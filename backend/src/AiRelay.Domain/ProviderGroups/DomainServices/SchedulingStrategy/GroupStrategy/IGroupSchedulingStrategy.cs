using AiRelay.Domain.ProviderGroups.Entities;

namespace AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;

/// <summary>
/// 分组账户调度策略接口
/// </summary>
public interface IGroupSchedulingStrategy
{
    /// <summary>
    /// 从可用账户列表中选择一个账户
    /// </summary>
    /// <param name="relations">可用的账户关联关系列表</param>
    /// <param name="usageProvider">获取账户使用计数的方法</param>
    /// <returns>选中的账户关联关系，如果没有可用账户则返回null</returns>
    Task<ProviderGroupAccountRelation?> SelectAccountAsync(
        IReadOnlyList<ProviderGroupAccountRelation> relations,
        Func<IEnumerable<Guid>, Task<Dictionary<Guid, long>>> usageProvider,
        IReadOnlyDictionary<Guid, int> concurrencyCounts);
}

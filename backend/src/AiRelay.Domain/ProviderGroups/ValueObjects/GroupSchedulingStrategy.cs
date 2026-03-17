namespace AiRelay.Domain.ProviderGroups.ValueObjects;

/// <summary>
/// 分组调度策略
/// </summary>
public enum GroupSchedulingStrategy
{
    /// <summary>
    /// 自适应均衡 - 基于剩余容量加权的最少使用策略 (推荐)
    /// </summary>
    AdaptiveBalanced = 1,

    /// <summary>
    /// 加权随机 - 根据权重随机选择账户
    /// </summary>
    WeightedRandom = 2,

    /// <summary>
    /// 优先级降级 - 按优先级顺序使用，高优先级不可用时降级
    /// </summary>
    Priority = 3,

    /// <summary>
    /// 配额优先 - 按配额降序、优先级升序选择账户
    /// </summary>
    QuotaPriority = 4
}

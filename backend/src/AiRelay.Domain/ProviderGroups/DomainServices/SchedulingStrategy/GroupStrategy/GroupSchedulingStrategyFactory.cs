using AiRelay.Domain.ProviderGroups.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.GroupStrategy;

/// <summary>
/// 调度策略工厂（统一使用 DI 创建策略实例）
/// </summary>
public class GroupSchedulingStrategyFactory(IServiceProvider serviceProvider)
{
    public IGroupSchedulingStrategy CreateStrategy(GroupSchedulingStrategy strategy)
    {
        return strategy switch
        {
            GroupSchedulingStrategy.WeightedRandom => serviceProvider.GetRequiredService<WeightedRandomStrategy>(),
            GroupSchedulingStrategy.AdaptiveBalanced => serviceProvider.GetRequiredService<AdaptiveBalancedStrategy>(),
            GroupSchedulingStrategy.Priority => serviceProvider.GetRequiredService<PriorityStrategy>(),
            // 使用 DI 容器获取 QuotaPriorityStrategy，确保其依赖 (RateLimitTracker等) 正确注入
            GroupSchedulingStrategy.QuotaPriority => serviceProvider.GetRequiredService<QuotaPriorityStrategy>(),
            _ => throw new ArgumentException($"不支持的调度策略: {strategy}", nameof(strategy))
        };
    }
}

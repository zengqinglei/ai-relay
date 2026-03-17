import { GroupSchedulingStrategy } from '../../features/platform/models/provider-group.dto';

/**
 * 调度策略显示标签映射
 */
export const SCHEDULING_STRATEGY_LABELS: Record<GroupSchedulingStrategy, string> = {
  [GroupSchedulingStrategy.AdaptiveBalanced]: '自适应均衡',
  [GroupSchedulingStrategy.WeightedRandom]: '加权随机',
  [GroupSchedulingStrategy.Priority]: '优先级降级',
  [GroupSchedulingStrategy.QuotaPriority]: '配额优先级'
};

/**
 * 调度策略下拉选项
 * 用于 PrimeNG Select 组件
 */
export const SCHEDULING_STRATEGY_OPTIONS = Object.entries(SCHEDULING_STRATEGY_LABELS).map(([value, label]) => ({
  label,
  value: value as GroupSchedulingStrategy
}));

import { Pipe, PipeTransform } from '@angular/core';

import { GroupSchedulingStrategy } from '../../features/platform/models/provider-group.dto';
import { SCHEDULING_STRATEGY_LABELS } from '../constants/scheduling-strategy.constants';

/**
 * 调度策略标签转换管道
 *
 * 将 GroupSchedulingStrategy 枚举值转换为中文显示标签
 *
 * 使用示例:
 * ```html
 * <td>{{ group.schedulingStrategy | schedulingStrategyLabel }}</td>
 * ```
 *
 * 输入: GroupSchedulingStrategy.WeightedRandom
 * 输出: "加权随机"
 */
@Pipe({
  name: 'schedulingStrategyLabel',
  standalone: true
})
export class SchedulingStrategyLabelPipe implements PipeTransform {
  transform(value: GroupSchedulingStrategy | string | undefined | null): string {
    if (!value) {
      return '';
    }
    return SCHEDULING_STRATEGY_LABELS[value as GroupSchedulingStrategy] || value;
  }
}

import { CommonModule, DecimalPipe } from '@angular/common';
import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { CardModule } from 'primeng/card';

import { formatTokenCount, formatNumber } from '../../../../../../shared/utils/format.utils';
import { AccountTokenMetricsOutputDto } from '../../../../models/account-token.dto';
import { SubscriptionMetricsOutputDto } from '../../../../models/subscription.dto';
import { UsageMetricsOutputDto } from '../../../../models/usage.dto';

/**
 * 指标卡片组件
 * 展示 Dashboard 的核心指标数据
 */
@Component({
  selector: 'app-metrics-cards',
  standalone: true,
  imports: [CardModule, CommonModule],
  templateUrl: './metrics-cards.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DecimalPipe]
})
export class MetricsCards {
  usage = input.required<UsageMetricsOutputDto>();
  accounts = input.required<AccountTokenMetricsOutputDto>();
  subscriptions = input.required<SubscriptionMetricsOutputDto>();

  /**
   * 获取趋势图标类名
   */
  getTrendIcon(trend: number): string {
    return trend >= 0 ? 'pi-arrow-up' : 'pi-arrow-down';
  }

  /**
   * 获取趋势样式类名
   */
  getTrendClass(trend: number): string {
    return trend >= 0 ? 'text-red-600 bg-red-500/12' : 'text-emerald-600 bg-emerald-500/12';
  }

  /**
   * 格式化数字（添加千位分隔符）
   */
  formatNumber(num: number): string {
    return formatNumber(num);
  }

  /**
   * 格式化 Token 数量（K, M, B）
   */
  formatTokenCount(num: number): string {
    return formatTokenCount(num);
  }

  /**
   * 安全地计算百分比
   */
  calculatePercentage(numerator: number, denominator: number): number {
    if (!denominator || denominator === 0) {
      return 0;
    }
    return (numerator / denominator) * 100;
  }

  Math = Math;
}

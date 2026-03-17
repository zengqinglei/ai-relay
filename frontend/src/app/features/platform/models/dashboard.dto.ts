import { AccountTokenMetricsOutputDto } from './account-token.dto';
import { SubscriptionMetricsOutputDto } from './subscription.dto';
import { UsageMetricsOutputDto, UsageTrendOutputDto, ApiKeyTrendOutputDto, ModelDistributionOutputDto } from './usage.dto';

/**
 * 纯前端聚合的 Dashboard ViewModel
 * 用于页面展示，不对应后端任何单一接口
 */
export interface DashboardViewModel {
  // 流量指标
  usage: UsageMetricsOutputDto;

  // Token使用趋势（24小时）
  trend: UsageTrendOutputDto[];

  // 模型使用分布（Top 10）
  modelDistribution: ModelDistributionOutputDto[];

  // API Key使用趋势（Top 10）
  apiKeyTrend: ApiKeyTrendOutputDto[];

  // 账户指标 (复用)
  accounts: AccountTokenMetricsOutputDto;

  // 订阅指标 (复用)
  subscriptions: SubscriptionMetricsOutputDto;
}

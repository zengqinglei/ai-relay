import { SubscriptionMetricsOutputDto } from '../../src/app/features/platform/models/subscription.dto';
import { MockRequest } from '../core/models';
import { SUBSCRIPTIONS } from '../data/subscriptions';

const subscriptions = [...SUBSCRIPTIONS];

function getMetrics(_req: MockRequest): SubscriptionMetricsOutputDto {
  return {
    totalSubscriptions: subscriptions.length,
    activeSubscriptions: subscriptions.filter(s => s.isActive).length,
    expiringSoon: 1,
    totalUsageToday: subscriptions.reduce((acc, curr) => acc + (curr.usageToday || 0), 0),
    usageGrowthRate: 5.2,
    topUsageKeys: subscriptions
      .map(s => ({ name: s.name, usage: s.usageToday || 0 }))
      .sort((a, b) => b.usage - a.usage)
      .slice(0, 5)
  };
}

export const SUBSCRIPTION_METRIC_API = {
  'GET /api/v1/api-keys/metrics': (req: MockRequest) => getMetrics(req)
};

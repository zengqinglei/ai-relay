import { SubscriptionMetricsOutputDto } from '../../src/app/features/platform/models/subscription.dto';
import { MockRequest } from '../core/models';
import { getSubscriptionsByUserId } from '../data/subscriptions';
import { getCurrentUserId } from '../utils/current-user';

function getMetrics(req: MockRequest): SubscriptionMetricsOutputDto {
  const currentUserId = getCurrentUserId(req);
  const subscriptions = getSubscriptionsByUserId(currentUserId);
  const now = Date.now();

  return {
    totalSubscriptions: subscriptions.length,
    activeSubscriptions: subscriptions.filter(s => s.isActive).length,
    expiringSoon: subscriptions.filter(s => {
      if (!s.expiresAt) {
        return false;
      }
      const diff = new Date(s.expiresAt).getTime() - now;
      return diff > 0 && diff <= 7 * 24 * 3600 * 1000;
    }).length,
    totalUsageToday: subscriptions.reduce((acc, curr) => acc + (curr.usageToday || 0), 0),
    usageGrowthRate: subscriptions.length > 0 ? 5.2 : 0,
    topUsageKeys: subscriptions
      .map(s => ({ name: s.name, usage: s.usageToday || 0, unit: '次' }))
      .sort((a, b) => b.usage - a.usage)
      .slice(0, 3)
  };
}

export const SUBSCRIPTION_METRIC_API = {
  'GET /api/v1/api-keys/metrics': (req: MockRequest) => getMetrics(req)
};

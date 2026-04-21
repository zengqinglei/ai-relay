import { MockRequest } from '../core/models';
import { ACCOUNT_TOKENS } from '../data/account-token';
import { getSubscriptionsByUserId } from '../data/subscriptions';
import { getCurrentUserId } from '../utils/current-user';

function getMetrics(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const subscriptions = getSubscriptionsByUserId(currentUserId);
  const groupIds = new Set(subscriptions.flatMap(item => item.bindings.map(binding => binding.providerGroupId)));
  const accounts = ACCOUNT_TOKENS.filter(account => account.providerGroupIds.some(groupId => groupIds.has(groupId)));

  const totalAccounts = accounts.length;
  const activeAccounts = accounts.filter(item => item.isActive).length;
  const disabledAccounts = totalAccounts - activeAccounts;
  const expiringAccounts = accounts.filter(item => item.expiresIn !== null && item.expiresIn !== undefined && item.expiresIn <= 3600).length;
  const totalUsageToday = accounts.reduce((sum, item) => sum + item.usageToday, 0);
  const averageSuccessRate = totalAccounts > 0
    ? Number((accounts.reduce((sum, item) => sum + item.successRateToday, 0) / totalAccounts).toFixed(1))
    : 0;
  const abnormalRequests24h = accounts.reduce((sum, item) => sum + Math.max(0, Math.round(item.usageToday * (100 - item.successRateToday) / 100)), 0);
  const rotationWarnings = accounts.filter(item => item.status !== 'Normal').length;

  return {
    totalAccounts,
    activeAccounts,
    disabledAccounts,
    expiringAccounts,
    totalUsageToday,
    usageGrowthRate: totalUsageToday > 0 ? 9.4 : 0,
    averageSuccessRate,
    abnormalRequests24h,
    rotationWarnings
  };
}

export const ACCOUNT_TOKEN_METRIC_API = {
  'GET /api/v1/account-tokens/metrics': (req: MockRequest) => getMetrics(req)
};

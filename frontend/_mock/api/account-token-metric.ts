import { MockRequest } from '../core/models';
import { ACCOUNT_METRICS } from '../data/account-token';

export const ACCOUNT_TOKEN_METRIC_API = {
  'GET /api/v1/account-tokens/metrics': (_req: MockRequest) => ACCOUNT_METRICS
};

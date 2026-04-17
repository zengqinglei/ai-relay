import { AuthMethod } from '../models/auth-method.enum';
import { Provider } from '../models/provider.enum';
import { RateLimitScope } from '../../features/platform/models/account-token.dto';

export const PROVIDER_LABELS: Record<Provider, string> = {
  [Provider.Gemini]: 'Gemini',
  [Provider.Claude]: 'Claude',
  [Provider.OpenAI]: 'OpenAI',
  [Provider.Antigravity]: 'Antigravity',
  [Provider.OpenAICompatible]: 'OpenAI Compatible'
};

export const PROVIDER_OPTIONS = Object.entries(PROVIDER_LABELS).map(([value, label]) => ({
  label,
  value: value as Provider
}));

export const AUTH_METHOD_LABELS: Record<AuthMethod, string> = {
  [AuthMethod.OAuth]: 'OAuth',
  [AuthMethod.ApiKey]: 'API Key'
};

export const AUTH_METHOD_OPTIONS = Object.entries(AUTH_METHOD_LABELS).map(([value, label]) => ({
  label,
  value: value as AuthMethod
}));

export const RATE_LIMIT_SCOPE_LABELS: Record<RateLimitScope, string> = {
  [RateLimitScope.Account]: '按账户',
  [RateLimitScope.Model]: '按模型'
};

export const RATE_LIMIT_SCOPE_OPTIONS = Object.entries(RATE_LIMIT_SCOPE_LABELS).map(([value, label]) => ({
  label,
  value: value as RateLimitScope
}));

/**
 * 获取 Provider + AuthMethod 组合的显示标签
 */
export function getProviderAuthLabel(provider: Provider, authMethod: AuthMethod): string {
  return `${PROVIDER_LABELS[provider]} ${AUTH_METHOD_LABELS[authMethod]}`;
}

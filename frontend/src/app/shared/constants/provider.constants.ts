import { AuthMethod } from '../models/auth-method.enum';
import { Provider } from '../models/provider.enum';

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

/**
 * 获取 Provider + AuthMethod 组合的显示标签
 */
export function getProviderAuthLabel(provider: Provider, authMethod: AuthMethod): string {
  return `${PROVIDER_LABELS[provider]} ${AUTH_METHOD_LABELS[authMethod]}`;
}

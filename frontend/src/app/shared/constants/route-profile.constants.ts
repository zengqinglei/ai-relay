import { AuthMethod } from '../models/auth-method.enum';
import { Provider } from '../models/provider.enum';
import { RouteProfile } from '../models/route-profile.enum';

export const ROUTE_PROFILE_LABELS: Record<RouteProfile, string> = {
  [RouteProfile.GeminiInternal]: 'Gemini Internal',
  [RouteProfile.GeminiBeta]: 'Gemini Beta',
  [RouteProfile.OpenAiResponses]: 'OpenAI Responses',
  [RouteProfile.OpenAiCodex]: 'OpenAI Codex',
  [RouteProfile.ChatCompletions]: 'Chat Completions',
  [RouteProfile.ClaudeMessages]: 'Claude Messages'
};

/** 带路径的完整标签，用于 tooltip */
export const ROUTE_PROFILE_FULL_LABELS: Record<RouteProfile, string> = {
  [RouteProfile.GeminiInternal]: 'Gemini Internal (/v1internal)',
  [RouteProfile.GeminiBeta]: 'Gemini Beta (/v1beta)',
  [RouteProfile.OpenAiResponses]: 'OpenAI Responses (/v1/responses)',
  [RouteProfile.OpenAiCodex]: 'OpenAI Codex (/backend-api/codex)',
  [RouteProfile.ChatCompletions]: 'Chat Completions (/v1/chat/completions)',
  [RouteProfile.ClaudeMessages]: 'Claude Messages (/v1/messages)'
};

export const ROUTE_PROFILE_OPTIONS = Object.entries(ROUTE_PROFILE_LABELS).map(([value, label]) => ({
  label,
  value: value as RouteProfile
}));

/**
 * RouteProfile 支持的 (Provider, AuthMethod) 组合
 */
export const ROUTE_PROFILE_SUPPORTED_COMBINATIONS: Record<RouteProfile, Array<{ provider: Provider; authMethod: AuthMethod }>> = {
  [RouteProfile.GeminiInternal]: [
    { provider: Provider.Gemini, authMethod: AuthMethod.OAuth }
  ],
  [RouteProfile.GeminiBeta]: [
    { provider: Provider.Gemini, authMethod: AuthMethod.OAuth },
    { provider: Provider.Gemini, authMethod: AuthMethod.ApiKey },
    { provider: Provider.Antigravity, authMethod: AuthMethod.OAuth }
  ],
  [RouteProfile.OpenAiResponses]: [
    { provider: Provider.OpenAI, authMethod: AuthMethod.OAuth },
    { provider: Provider.OpenAI, authMethod: AuthMethod.ApiKey }
  ],
  [RouteProfile.OpenAiCodex]: [
    { provider: Provider.OpenAI, authMethod: AuthMethod.OAuth }
  ],
  [RouteProfile.ChatCompletions]: [
    { provider: Provider.OpenAI, authMethod: AuthMethod.OAuth },
    { provider: Provider.OpenAI, authMethod: AuthMethod.ApiKey }
  ],
  [RouteProfile.ClaudeMessages]: [
    { provider: Provider.Claude, authMethod: AuthMethod.OAuth },
    { provider: Provider.Claude, authMethod: AuthMethod.ApiKey },
    { provider: Provider.Antigravity, authMethod: AuthMethod.OAuth }
  ]
};

import { ProviderPlatform } from '../models/provider-platform.enum';

export const PROVIDER_PLATFORM_LABELS: Record<ProviderPlatform, string> = {
  [ProviderPlatform.GEMINI_OAUTH]: 'Gemini OAuth',
  [ProviderPlatform.GEMINI_APIKEY]: 'Gemini Api Key',
  [ProviderPlatform.CLAUDE_OAUTH]: 'Claude OAuth',
  [ProviderPlatform.CLAUDE_APIKEY]: 'Claude Api Key',
  [ProviderPlatform.OPENAI_OAUTH]: 'OpenAI OAuth',
  [ProviderPlatform.OPENAI_APIKEY]: 'OpenAI Api Key',

  // Antigravity 平台
  [ProviderPlatform.ANTIGRAVITY]: 'Antigravity'
};

export const PROVIDER_PLATFORM_OPTIONS = Object.entries(PROVIDER_PLATFORM_LABELS).map(([value, label]) => ({
  label,
  value: value as ProviderPlatform
}));

import { ModelVendor } from '../models/model-vendor.enum';

export const MODEL_VENDOR_LABELS: Record<ModelVendor, string> = {
  [ModelVendor.Google]: 'Google',
  [ModelVendor.Anthropic]: 'Anthropic',
  [ModelVendor.OpenAI]: 'OpenAI',
  [ModelVendor.Qwen]: '通义千问',
  [ModelVendor.Moonshot]: 'Moonshot',
  [ModelVendor.DeepSeek]: 'DeepSeek',
  [ModelVendor.MiniMax]: 'MiniMax',
  [ModelVendor.Zhipu]: 'Zhipu',
  [ModelVendor.Jimeng]: '即梦'
};

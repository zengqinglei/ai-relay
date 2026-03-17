import { ACCOUNT_TOKENS } from './account-token';
import { PROVIDER_GROUPS } from './provider-group';
import { UsageRecordOutputDto } from '../../src/app/features/platform/models/usage.dto';
import { UsageStatus } from '../../src/app/shared/models/usage-status.enum';

export interface MockUsageRecord extends UsageRecordOutputDto {
  accountTokenId: string;
  providerGroupId: string;
}

const MODELS = [
  'gpt-4o',
  'gpt-4-turbo',
  'gpt-3.5-turbo',
  'claude-3-5-sonnet-20240620',
  'claude-3-opus-20240229',
  'claude-3-haiku-20240307',
  'gemini-1.5-pro',
  'gemini-1.5-flash',
  'gemini-pro-vision'
];
const PATHS = ['/v1/chat/completions', '/v1/completions', '/v1/embeddings', '/v1/images/generations', '/v1/audio/speech'];
const METHODS = ['POST']; // AI APIs are mostly POST, but could add GET for specific paths if any
const API_KEYS = ['sk-prod-xc9...', 'sk-test-8s2...', 'sk-dev-m2k...', 'sk-vip-9ln...', 'sk-internal-5pq...'];
const USER_AGENTS = [
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36',
  'PostmanRuntime/7.39.0',
  'OpenAI/v1 PythonBindings/1.35.0',
  'LangChain/0.2.5 (Python)',
  'curl/8.7.1'
];

// Generate dummy usage records
const generateRecords = (count: number): MockUsageRecord[] => {
  return Array.from({ length: count })
    .map((_, i) => {
      const isStreaming = Math.random() > 0.3; // 70% streaming
      const model = MODELS[Math.floor(Math.random() * MODELS.length)];

      // Generate 3 status types with realistic distribution
      const statusRandom = Math.random();
      let status: string;
      let upStatusCode: number | undefined;
      let statusDescription: string | undefined;

      if (statusRandom < 0.85) {
        // 85% Success
        status = UsageStatus.Success;
        upStatusCode = 200;
      } else if (statusRandom < 0.9) {
        // 5% Failed - 切换账号
        status = UsageStatus.Failed;
        upStatusCode = 429;
        statusDescription = '切换账号: Rate limit exceeded';
      } else if (statusRandom < 0.95) {
        // 5% Failed - 响应已开始
        status = UsageStatus.Failed;
        upStatusCode = 500;
        statusDescription = '响应已开始后发生错误';
      } else if (statusRandom < 0.98) {
        // 3% Failed - 其他错误
        status = UsageStatus.Failed;
        upStatusCode = 500;
        statusDescription = 'Internal server error';
      } else {
        // 2% InProgress (进行中)
        status = UsageStatus.InProgress;
        upStatusCode = undefined;
      }

      const inputTokens = Math.floor(Math.random() * 2000) + 50;
      const outputTokens = Math.floor(Math.random() * 4000) + 10;

      // More realistic cost calculation (approximate)
      const costPer1k = 0.01;
      const finalCost = ((inputTokens + outputTokens) / 1000) * costPer1k * (Math.random() * 0.5 + 0.8);

      // Pick random account and group
      const account = ACCOUNT_TOKENS[Math.floor(Math.random() * ACCOUNT_TOKENS.length)];
      const group = PROVIDER_GROUPS[Math.floor(Math.random() * PROVIDER_GROUPS.length)];

      return {
        id: `rec-${Date.now()}-${i}`,
        creationTime: new Date(Date.now() - Math.floor(Math.random() * 7 * 24 * 3600 * 1000)).toISOString(),
        apiKeyName: API_KEYS[Math.floor(Math.random() * API_KEYS.length)],
        platform: account.platform,
        providerGroupName: group.name,
        providerGroupId: group.id,
        accountTokenName: account.name,
        accountTokenId: account.id,
        downModelId: model,
        upModelId: model,
        downRequestUrl: PATHS[Math.floor(Math.random() * PATHS.length)],
        downRequestMethod: METHODS[0],
        isStreaming: isStreaming,
        downUserAgent: USER_AGENTS[Math.floor(Math.random() * USER_AGENTS.length)],
        upUserAgent: 'AiRelay/1.0',
        inputTokens: inputTokens,
        outputTokens: outputTokens,
        cacheReadTokens: Math.random() > 0.8 ? Math.floor(Math.random() * 100) : 0,
        cacheCreationTokens: 0,
        downClientIp: `203.0.113.${Math.floor(Math.random() * 255)}`,
        finalCost: finalCost,
        status: status,
        upStatusCode: upStatusCode,
        durationMs: Math.floor(Math.random() * 10000) + 200,
        statusDescription: statusDescription
      };
    })
    .sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime());
};

export const USAGE_RECORDS = generateRecords(200);

export const getUsageRecordDetail = (id: string) => {
  const record = USAGE_RECORDS.find(r => r.id === id);
  if (!record) return null;

  return {
    usageRecordId: record.id,
    downRequestUrl: record.downRequestUrl,
    downRequestHeaders: '{"User-Agent": "PostmanRuntime/7.39.0", "Content-Type": "application/json"}',
    downRequestBody: '{"model": "gpt-4o", "messages": [{"role": "user", "content": "Hello!"}]}',
    downResponseBody:
      '{"id": "chatcmpl-123", "object": "chat.completion", "created": 1677652288, "model": "gpt-4o-0613", "choices": [{"index": 0, "message": {"role": "assistant", "content": "Hello there, how may I assist you today?"}, "finish_reason": "stop"}]}',
    upRequestUrl: `https://api.openai.com${record.downRequestUrl}`,
    upRequestHeaders: '{"Authorization": "Bearer sk-proj-...", "Content-Type": "application/json"}',
    upRequestBody: '{"model": "gpt-4o", "messages": [{"role": "user", "content": "Hello!"}]}',
    upResponseBody:
      '{"id": "chatcmpl-123", "object": "chat.completion", "created": 1677652288, "model": "gpt-4o-0613", "choices": [{"index": 0, "message": {"role": "assistant", "content": "Hello there, how may I assist you today?"}, "finish_reason": "stop"}]}',
    upStatusCode: record.upStatusCode
  };
};

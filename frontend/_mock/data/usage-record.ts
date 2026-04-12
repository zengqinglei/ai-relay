import { ACCOUNT_TOKENS } from './account-token';
import { PROVIDER_GROUPS } from './provider-group';
import { UsageRecordOutputDto } from '../../src/app/features/platform/models/usage.dto';
import { UsageStatus } from '../../src/app/shared/models/usage-status.enum';
import { AuthMethod } from '../../src/app/shared/models/auth-method.enum';

export interface MockUsageRecord extends UsageRecordOutputDto {
  accountTokenId: string;
  providerGroupId: string;
  authMethod: AuthMethod;
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
const METHODS = ['POST'];
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
      const isStreaming = Math.random() > 0.3;
      const model = MODELS[Math.floor(Math.random() * MODELS.length)];

      const statusRandom = Math.random();
      let status: string;
      let statusDescription: string | undefined;
      let attemptCount: number;

      if (statusRandom < 0.85) {
        status = UsageStatus.Success;
        attemptCount = Math.random() > 0.8 ? Math.floor(Math.random() * 3) + 2 : 1;
      } else if (statusRandom < 0.9) {
        status = UsageStatus.Failed;
        statusDescription = '切换账号: Rate limit exceeded';
        attemptCount = Math.floor(Math.random() * 3) + 2;
      } else if (statusRandom < 0.95) {
        status = UsageStatus.Failed;
        statusDescription = '响应已开始后发生错误';
        attemptCount = 1;
      } else if (statusRandom < 0.98) {
        status = UsageStatus.Failed;
        statusDescription = 'Internal server error';
        attemptCount = Math.floor(Math.random() * 2) + 1;
      } else {
        status = UsageStatus.InProgress;
        attemptCount = 1;
      }

      const inputTokens = Math.floor(Math.random() * 2000) + 50;
      const outputTokens = Math.floor(Math.random() * 4000) + 10;
      const costPer1k = 0.01;
      const finalCost = ((inputTokens + outputTokens) / 1000) * costPer1k * (Math.random() * 0.5 + 0.8);

      const account = ACCOUNT_TOKENS[Math.floor(Math.random() * ACCOUNT_TOKENS.length)];
      const group = PROVIDER_GROUPS[Math.floor(Math.random() * PROVIDER_GROUPS.length)];

      return {
        id: `rec-${Date.now()}-${i}`,
        creationTime: new Date(Date.now() - Math.floor(Math.random() * 7 * 24 * 3600 * 1000)).toISOString(),
        apiKeyName: API_KEYS[Math.floor(Math.random() * API_KEYS.length)],
        sessionId: `sess-${Math.random().toString(36).substring(2, 10)}`,
        providerGroupName: group.name,
        providerGroupId: group.id,
        accountTokenName: account.name,
        accountTokenId: account.id,
        provider: account.provider,
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
        upStatusCode: status === UsageStatus.Success ? 200 : status === UsageStatus.InProgress ? undefined : 429,
        downStatusCode: status === UsageStatus.Success ? 200 : status === UsageStatus.InProgress ? undefined : 500,
        durationMs: Math.floor(Math.random() * 10000) + 200,
        statusDescription: statusDescription,
        attemptCount: attemptCount,
        authMethod: account.authMethod
      };
    })
    .sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime());
};

export const USAGE_RECORDS = generateRecords(200);

export const getUsageRecordDetail = (id: string) => {
  const record = USAGE_RECORDS.find(r => r.id === id);
  if (!record) return null;

  let currentStartTime = new Date(record.creationTime);
  const attempts = Array.from({ length: record.attemptCount }).map((_, i) => {
    const isLast = i === record.attemptCount - 1;
    const attemptStatus = isLast ? record.status : UsageStatus.Failed;
    const duration = Math.floor((record.durationMs ?? 1000) / record.attemptCount);
    
    const attempt = {
      attemptNumber: i + 1,
      startTime: currentStartTime.toISOString(),
      endTime: new Date(currentStartTime.getTime() + duration).toISOString(),
      provider: record.provider!,
      authMethod: record.authMethod,
      accountTokenName: record.accountTokenName ?? '',
      upModelId: record.downModelId,
      upUserAgent: 'AiRelay/1.0',
      upRequestUrl: `https://api.openai.com${record.downRequestUrl}`,
      upStatusCode: isLast && record.status === UsageStatus.Success ? 200 : 429,
      durationMs: duration,
      status: attemptStatus,
      statusDescription: !isLast ? 'Rate limit exceeded, switching account' : record.statusDescription,
      upRequestHeaders: '{"Authorization": "Bearer sk-proj-...", "Content-Type": "application/json"}',
      upRequestBody: '{"model": "gpt-4o", "messages": [{"role": "user", "content": "Hello!"}]}',
      upResponseBody: isLast
        ? '{"id": "chatcmpl-123", "object": "chat.completion", "created": 1677652288, "model": "gpt-4o", "choices": [{"index": 0, "message": {"role": "assistant", "content": "Hello there!"}, "finish_reason": "stop"}]}'
        : '{"error": {"message": "Rate limit exceeded", "type": "rate_limit_error"}}'
    };

    // 为下一次尝试更新时间（本次开始时间 + 本次耗时 + 随机网络延迟）
    currentStartTime = new Date(currentStartTime.getTime() + duration + 500);
    return attempt;
  });

  return {
    usageRecordId: record.id,
    downRequestUrl: record.downRequestUrl,
    downRequestHeaders: '{"User-Agent": "PostmanRuntime/7.39.0", "Content-Type": "application/json"}',
    downRequestBody: '{"model": "gpt-4o", "messages": [{"role": "user", "content": "Hello!"}]}',
    downResponseBody:
      '{"id": "chatcmpl-123", "object": "chat.completion", "created": 1677652288, "model": "gpt-4o", "choices": [{"index": 0, "message": {"role": "assistant", "content": "Hello there, how may I assist you today?"}, "finish_reason": "stop"}]}',
    attempts
  };
};

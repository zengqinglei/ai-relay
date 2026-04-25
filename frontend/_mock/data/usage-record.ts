import { ACCOUNT_TOKENS } from './account-token';
import { PROVIDER_GROUPS } from './provider-group';
import { getSubscriptionsByUserId } from './subscriptions';
import { USERS } from './user';
import { UsageRecordOutputDto, UsageRecordDetailOutputDto } from '../../src/app/features/platform/models/usage.dto';
import { UsageStatus } from '../../src/app/shared/models/usage-status.enum';
import { AuthMethod } from '../../src/app/shared/models/auth-method.enum';

export interface MockUsageRecord extends UsageRecordOutputDto {
  accountTokenId: string;
  providerGroupId: string;
  authMethod: AuthMethod;
  apiKeyId: string;
  userId: string;
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
const USER_AGENTS = [
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36',
  'PostmanRuntime/7.39.0',
  'OpenAI/v1 PythonBindings/1.35.0',
  'LangChain/0.2.5 (Python)',
  'curl/8.7.1'
];

const RECORD_COUNTS_BY_USER: Record<string, number> = {
  '00000000-0000-0000-0000-000000000001': 140,
  '00000000-0000-0000-0000-000000000002': 90
};

function seededNumber(seed: number) {
  const x = Math.sin(seed) * 10000;
  return x - Math.floor(x);
}

function generateRecordsForUser(userId: string, count: number): MockUsageRecord[] {
  const user = USERS.find(item => item.id === userId)!;
  const userSubscriptions = getSubscriptionsByUserId(userId);

  return Array.from({ length: count }).map((_, i) => {
    const seed = Number(userId.slice(-4)) + i * 17;
    const apiKey = userSubscriptions[i % userSubscriptions.length];
    const isStreaming = seededNumber(seed + 1) > 0.28;
    const model = MODELS[Math.floor(seededNumber(seed + 2) * MODELS.length)];

    const statusRoll = seededNumber(seed + 3);
    let status: string;
    let statusDescription: string | undefined;
    let attemptCount: number;

    if (statusRoll < 0.83) {
      status = UsageStatus.Success;
      attemptCount = seededNumber(seed + 4) > 0.8 ? 2 : 1;
    } else if (statusRoll < 0.91) {
      status = UsageStatus.Failed;
      statusDescription = '切换账号: Rate limit exceeded';
      attemptCount = 2;
    } else if (statusRoll < 0.97) {
      status = UsageStatus.Failed;
      statusDescription = 'Internal server error';
      attemptCount = 1;
    } else {
      status = UsageStatus.InProgress;
      attemptCount = 1;
    }

    const inputTokens = 120 + Math.floor(seededNumber(seed + 5) * 2100);
    const outputTokens = 80 + Math.floor(seededNumber(seed + 6) * 4200);
    const finalCost = Number((((inputTokens + outputTokens) / 1000) * (0.008 + seededNumber(seed + 7) * 0.006)).toFixed(6));

    const account = ACCOUNT_TOKENS[Math.floor(seededNumber(seed + 8) * ACCOUNT_TOKENS.length)];
    const group = PROVIDER_GROUPS[Math.floor(seededNumber(seed + 9) * PROVIDER_GROUPS.length)];
    const createdAt = new Date(Date.now() - Math.floor(seededNumber(seed + 10) * 14 * 24 * 3600 * 1000) - i * 1800000).toISOString();

    return {
      id: `rec-${userId.slice(-4)}-${i + 1}`,
      creationTime: createdAt,
      apiKeyId: apiKey.id,
      userId,
      username: user.username,
      email: user.email,
      apiKeyName: apiKey.name,
      sessionId: `sess-${userId.slice(-4)}-${Math.floor(seededNumber(seed + 11) * 100000)}`,
      providerGroupName: group.name,
      providerGroupId: group.id,
      accountTokenName: account.name,
      accountTokenId: account.id,
      provider: account.provider,
      downModelId: model,
      upModelId: model,
      downRequestUrl: PATHS[Math.floor(seededNumber(seed + 12) * PATHS.length)],
      downRequestMethod: METHODS[0],
      isStreaming,
      downUserAgent: USER_AGENTS[Math.floor(seededNumber(seed + 13) * USER_AGENTS.length)],
      upUserAgent: 'AiRelay/1.0',
      inputTokens,
      outputTokens,
      cacheReadTokens: seededNumber(seed + 14) > 0.8 ? Math.floor(seededNumber(seed + 15) * 120) : 0,
      cacheCreationTokens: 0,
      downClientIp: `203.0.113.${10 + (i % 200)}`,
      finalCost,
      status,
      upStatusCode: status === UsageStatus.Success ? 200 : status === UsageStatus.InProgress ? undefined : 429,
      downStatusCode: status === UsageStatus.Success ? 200 : status === UsageStatus.InProgress ? undefined : 500,
      durationMs: 250 + Math.floor(seededNumber(seed + 16) * 9000),
      statusDescription,
      attemptCount,
      authMethod: account.authMethod
    };
  });
}

export const USAGE_RECORDS: MockUsageRecord[] = Object.entries(RECORD_COUNTS_BY_USER)
  .flatMap(([userId, count]) => generateRecordsForUser(userId, count))
  .sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime());

export function getUsageRecordsByUserId(userId: string) {
  return USAGE_RECORDS.filter(item => item.userId === userId);
}

export function getAllUsageRecords() {
  return USAGE_RECORDS;
}

export const getUsageRecordDetail = (id: string): UsageRecordDetailOutputDto | null => {
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

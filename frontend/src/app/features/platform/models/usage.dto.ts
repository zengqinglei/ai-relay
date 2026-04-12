import { AuthMethod } from '../../../shared/models/auth-method.enum';
import { PagedRequestDto } from '../../../shared/models/paged-request.dto';
import { Provider } from '../../../shared/models/provider.enum';

export interface UsageMetricsOutputDto {
  totalRequests: number;
  requestsTrend: number;
  currentRps: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalCost: number;
  successRequests: number;
  failedRequests: number;
}

export interface UsageTrendOutputDto {
  time: string;
  requests: number;
  inputTokens: number;
  outputTokens: number;
}

export interface ApiKeyTrendOutputDto {
  apiKeyName: string;
  trend: UsageTrendOutputDto[];
  totalRequests: number;
}

export interface ModelDistributionOutputDto {
  model: string;
  requestCount: number;
  totalTokens: number;
  totalCost: number;
  percentage: number;
}

export interface UsageRecordOutputDto {
  id: string;
  creationTime: string;
  apiKeyName: string;
  sessionId: string;
  provider?: Provider;
  providerGroupName?: string;
  accountTokenName?: string;
  downModelId?: string;
  upModelId?: string;
  downRequestUrl: string;
  downRequestMethod: string;
  isStreaming: boolean;
  downUserAgent: string;
  inputTokens?: number;
  outputTokens?: number;
  cacheReadTokens?: number;
  cacheCreationTokens?: number;
  downClientIp: string;
  finalCost?: number;
  status: string;
  upStatusCode?: number;
  downStatusCode?: number;
  durationMs?: number;
  statusDescription?: string;
  attemptCount: number;
  authMethod?: AuthMethod;
}

export interface UsageRecordPagedInputDto extends PagedRequestDto {
  apiKeyName?: string;
  model?: string;
  accountTokenName?: string;
  sessionId?: string;
  providerGroupId?: string;
  provider?: Provider;
  startTime?: string;
  endTime?: string;
  authMethod?: AuthMethod;
}

export interface UsageRecordAttemptOutputDto {
  attemptNumber: number;
  startTime: string;
  endTime: string;
  provider: Provider;
  authMethod: AuthMethod;
  accountTokenName: string;
  upModelId?: string;
  upUserAgent?: string;
  upRequestUrl?: string;
  upStatusCode?: number;
  durationMs: number;
  status: string;
  statusDescription?: string;
  upRequestHeaders?: string;
  upRequestBody?: string;
  upResponseBody?: string;
}

export interface UsageRecordDetailOutputDto {
  usageRecordId: string;
  downRequestUrl?: string;
  downRequestHeaders?: string;
  downRequestBody?: string;
  downResponseBody?: string;
  attempts: UsageRecordAttemptOutputDto[];
}

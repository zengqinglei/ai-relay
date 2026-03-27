import { PagedRequestDto } from '../../../shared/models/paged-request.dto';
import { ProviderPlatform } from '../../../shared/models/provider-platform.enum';

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
  platform: ProviderPlatform;
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
}

export interface UsageRecordPagedInputDto extends PagedRequestDto {
  apiKeyName?: string;
  model?: string;
  accountTokenName?: string;
  providerGroupId?: string;
  platform?: ProviderPlatform;
  startTime?: string;
  endTime?: string;
}

export interface UsageRecordAttemptOutputDto {
  attemptNumber: number;
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

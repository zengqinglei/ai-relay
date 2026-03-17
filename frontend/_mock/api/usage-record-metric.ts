import {
  UsageMetricsOutputDto,
  UsageTrendOutputDto,
  ApiKeyTrendOutputDto,
  ModelDistributionOutputDto
} from '../../src/app/features/platform/models/usage.dto';

// 流量指标数据
const metrics: UsageMetricsOutputDto = {
  totalRequests: 12580,
  requestsTrend: 15.4,
  currentRps: 45.2,
  totalInputTokens: 1250000,
  totalOutputTokens: 850000,
  totalCost: 12.5678,
  successRequests: 12100,
  failedRequests: 480
};

// 24小时趋势数据（含Token）
const trend: UsageTrendOutputDto[] = Array.from({ length: 24 }).map((_, i) => ({
  time: `${String(i).padStart(2, '0')}:00`,
  requests: Math.floor(Math.random() * 1000) + 100,
  inputTokens: Math.floor(Math.random() * 50000) + 10000,
  outputTokens: Math.floor(Math.random() * 30000) + 5000
}));

// Top 7 API Key 趋势数据
const apiKeyNames = [
  'Production-API-Key',
  'Development-API-Key',
  'Testing-API-Key',
  'Staging-API-Key',
  'Demo-API-Key',
  'Partner-API-Key',
  'Internal-API-Key'
];

const topApiKeys: ApiKeyTrendOutputDto[] = apiKeyNames.map((name, index) => ({
  apiKeyName: name,
  totalRequests: Math.floor(Math.random() * 5000) + 1000 - index * 200,
  trend: Array.from({ length: 24 }).map((_, i) => ({
    time: `${String(i).padStart(2, '0')}:00`,
    requests: Math.floor(Math.random() * 200) + 50 - index * 10,
    inputTokens: Math.floor(Math.random() * 10000) + 2000,
    outputTokens: Math.floor(Math.random() * 6000) + 1000
  }))
}));

// Top 7 模型分布数据
const models = ['gpt-4o', 'gpt-4-turbo', 'gpt-3.5-turbo', 'claude-3-5-sonnet', 'claude-3-opus', 'gemini-pro', 'gemini-1.5-pro'];

const totalModelRequests = 12580;
const modelDistribution: ModelDistributionOutputDto[] = models.map((model, index) => {
  const requestCount = Math.floor(Math.random() * 2000) + 500 - index * 100;
  const percentage = (requestCount / totalModelRequests) * 100;
  return {
    model,
    requestCount,
    totalTokens: requestCount * (Math.floor(Math.random() * 2000) + 1000),
    totalCost: requestCount * (Math.random() * 0.01 + 0.001),
    percentage: Number(percentage.toFixed(2))
  };
});

export const usageQueryApi = {
  'GET /api/v1/usage/query/metrics': () => metrics,
  'GET /api/v1/usage/query/trend': () => trend,
  'GET /api/v1/usage/query/top-api-keys': () => topApiKeys,
  'GET /api/v1/usage/query/model-distribution': () => modelDistribution
};

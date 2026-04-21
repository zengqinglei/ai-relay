import {
  UsageMetricsOutputDto,
  UsageTrendOutputDto,
  ApiKeyTrendOutputDto,
  ModelDistributionOutputDto
} from '../../src/app/features/platform/models/usage.dto';
import { MockRequest } from '../core/models';
import { getUsageRecordsByUserId } from '../data/usage-record';
import { getCurrentUserId } from '../utils/current-user';

function getDateRange(req: MockRequest) {
  const start = req.queryParams['startTime'] ? new Date(String(req.queryParams['startTime'])) : undefined;
  const end = req.queryParams['endTime'] ? new Date(String(req.queryParams['endTime'])) : undefined;
  return { start, end };
}

function filterRecords(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const { start, end } = getDateRange(req);

  return getUsageRecordsByUserId(currentUserId).filter(record => {
    const time = new Date(record.creationTime).getTime();
    if (start && time < start.getTime()) {
      return false;
    }
    if (end && time > end.getTime()) {
      return false;
    }
    return true;
  });
}

function createBuckets(start: Date, end: Date) {
  const diffMs = Math.max(end.getTime() - start.getTime(), 1);
  const diffHours = diffMs / (1000 * 60 * 60);
  const stepMs = diffHours <= 24 ? 60 * 60 * 1000 : 24 * 60 * 60 * 1000;
  const buckets: Array<{ start: number; end: number; labelTime: string }> = [];

  let cursor = new Date(start);
  if (stepMs === 24 * 60 * 60 * 1000) {
    cursor = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate());
  } else {
    cursor = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate(), cursor.getHours());
  }

  while (cursor.getTime() <= end.getTime()) {
    const next = new Date(cursor.getTime() + stepMs);
    buckets.push({ start: cursor.getTime(), end: next.getTime(), labelTime: cursor.toISOString() });
    cursor = next;
    if (buckets.length > 64) {
      break;
    }
  }

  if (buckets.length === 0) {
    buckets.push({ start: start.getTime(), end: end.getTime() + 1, labelTime: start.toISOString() });
  }

  return buckets;
}

function findBucketIndex(time: Date, buckets: Array<{ start: number; end: number }>) {
  const timestamp = time.getTime();
  return buckets.findIndex((bucket, index) => {
    const isLast = index === buckets.length - 1;
    return timestamp >= bucket.start && (timestamp < bucket.end || (isLast && timestamp <= bucket.end));
  });
}

function buildTrend(records: ReturnType<typeof filterRecords>, start: Date, end: Date): UsageTrendOutputDto[] {
  const buckets = createBuckets(start, end);
  const trend = buckets.map(bucket => ({
    time: bucket.labelTime,
    requests: 0,
    inputTokens: 0,
    outputTokens: 0
  }));

  records.forEach(record => {
    const index = findBucketIndex(new Date(record.creationTime), buckets);
    if (index < 0) {
      return;
    }

    trend[index].requests += 1;
    trend[index].inputTokens += record.inputTokens || 0;
    trend[index].outputTokens += record.outputTokens || 0;
  });

  return trend;
}

function resolveRange(records: ReturnType<typeof filterRecords>, req: MockRequest) {
  const { start, end } = getDateRange(req);
  const now = new Date();
  const fallbackStart = start ?? new Date(now.getTime() - 24 * 60 * 60 * 1000);
  const fallbackEnd = end ?? now;

  if (records.length === 0) {
    return { start: fallbackStart, end: fallbackEnd };
  }

  const sorted = [...records].sort((a, b) => new Date(a.creationTime).getTime() - new Date(b.creationTime).getTime());
  return {
    start: start ?? new Date(sorted[0].creationTime),
    end: end ?? new Date(sorted[sorted.length - 1].creationTime)
  };
}

function getMetrics(req: MockRequest): UsageMetricsOutputDto {
  const records = filterRecords(req);
  const totalRequests = records.length;
  const successRequests = records.filter(item => item.status === 'Success').length;
  const failedRequests = records.filter(item => item.status === 'Failed').length;
  const totalInputTokens = records.reduce((sum, item) => sum + (item.inputTokens || 0), 0);
  const totalOutputTokens = records.reduce((sum, item) => sum + (item.outputTokens || 0), 0);
  const totalCost = records.reduce((sum, item) => sum + (item.finalCost || 0), 0);

  return {
    totalRequests,
    requestsTrend: totalRequests > 0 ? 8.6 : 0,
    currentRps: Number((totalRequests / 24).toFixed(1)),
    totalInputTokens,
    totalOutputTokens,
    totalCost: Number(totalCost.toFixed(4)),
    successRequests,
    failedRequests
  };
}

function getTrend(req: MockRequest): UsageTrendOutputDto[] {
  const records = filterRecords(req);
  const range = resolveRange(records, req);
  return buildTrend(records, range.start, range.end);
}

function getTopApiKeys(req: MockRequest): ApiKeyTrendOutputDto[] {
  const records = filterRecords(req);
  const range = resolveRange(records, req);
  const groups = new Map<string, typeof records>();

  records.forEach(record => {
    const key = record.apiKeyName || '未命名密钥';
    const current = groups.get(key) ?? [];
    current.push(record);
    groups.set(key, current);
  });

  return Array.from(groups.entries())
    .sort((a, b) => b[1].length - a[1].length)
    .slice(0, 7)
    .map(([apiKeyName, items]) => ({
      apiKeyName,
      totalRequests: items.length,
      trend: buildTrend(items, range.start, range.end)
    }));
}

function getModelDistribution(req: MockRequest): ModelDistributionOutputDto[] {
  const records = filterRecords(req);
  const total = records.length || 1;
  const groups = new Map<string, { requestCount: number; totalTokens: number; totalCost: number }>();

  records.forEach(record => {
    const model = record.downModelId || record.upModelId || '未识别模型';
    const current = groups.get(model) ?? { requestCount: 0, totalTokens: 0, totalCost: 0 };
    current.requestCount += 1;
    current.totalTokens += (record.inputTokens || 0) + (record.outputTokens || 0);
    current.totalCost += record.finalCost || 0;
    groups.set(model, current);
  });

  return Array.from(groups.entries())
    .map(([model, item]) => ({
      model,
      requestCount: item.requestCount,
      totalTokens: item.totalTokens,
      totalCost: Number(item.totalCost.toFixed(4)),
      percentage: Number(((item.requestCount / total) * 100).toFixed(2))
    }))
    .sort((a, b) => b.requestCount - a.requestCount)
    .slice(0, 7);
}

export const usageQueryApi = {
  'GET /api/v1/usage/query/metrics': (req: MockRequest) => getMetrics(req),
  'GET /api/v1/usage/query/trend': (req: MockRequest) => getTrend(req),
  'GET /api/v1/usage/query/top-api-keys': (req: MockRequest) => getTopApiKeys(req),
  'GET /api/v1/usage/query/model-distribution': (req: MockRequest) => getModelDistribution(req)
};

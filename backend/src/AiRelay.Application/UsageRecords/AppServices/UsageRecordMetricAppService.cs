using System.Linq.Dynamic.Core;
using AiRelay.Application.UsageRecords.Dtos.Query;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Application.UsageRecords.AppServices;

/// <summary>
/// 使用记录指标应用服务实现
/// </summary>
public class UsageRecordMetricAppService(
    IRepository<UsageRecord, Guid> usageRecordRepository,
    IQueryableAsyncExecuter asyncExecuter) : BaseAppService, IUsageRecordMetricAppService
{
    public async Task<UsageMetricsOutputDto> GetMetricsAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var end = endTime ?? now;
        var start = startTime ?? now.Date;
        var previousPeriodStart = start.AddDays(-(end - start).Days);
        var previousPeriodEnd = start;

        var query = await usageRecordRepository.GetQueryableAsync(cancellationToken);

        // 1. 当前周期总请求与消耗
        var currentStats = await asyncExecuter.SingleOrDefaultAsync(query
            .Where(r => r.CreationTime >= start && r.CreationTime < end)
            .GroupBy(r => 1)
            .Select(g => new
            {
                TotalRequests = g.Count(),
                TotalInputTokens = g.Sum(r => (long)(r.InputTokens ?? 0)),
                TotalOutputTokens = g.Sum(r => (long)(r.OutputTokens ?? 0)),
                TotalCost = g.Sum(r => r.FinalCost),
                SuccessRequests = g.Count(r => r.Status == UsageStatus.Success),
                FailedRequests = g.Count(r => r.Status == UsageStatus.Failed)
            }), cancellationToken);

        // 2. 上一周期总请求 (用于计算趋势)
        var previousRequests = await usageRecordRepository.CountAsync(
            r => r.CreationTime >= previousPeriodStart && r.CreationTime < previousPeriodEnd, cancellationToken);

        var currentRequests = currentStats?.TotalRequests ?? 0;

        // 3. 计算趋势
        decimal trend = 0;
        if (previousRequests > 0)
        {
            trend = ((decimal)currentRequests - previousRequests) / previousRequests * 100;
        }
        else if (currentRequests > 0)
        {
            trend = 100;
        }

        // 4. 计算 RPS (最近1分钟平均值)
        var oneMinuteAgo = now.AddMinutes(-1);
        var requestsLastMinute = await usageRecordRepository.CountAsync(
            r => r.CreationTime >= oneMinuteAgo, cancellationToken);
        var rps = Math.Round((decimal)requestsLastMinute / 60, 2);

        return new UsageMetricsOutputDto
        {
            TotalRequests = currentRequests,
            RequestsTrend = Math.Round(trend, 1),
            CurrentRps = rps,
            TotalInputTokens = currentStats?.TotalInputTokens ?? 0,
            TotalOutputTokens = currentStats?.TotalOutputTokens ?? 0,
            TotalCost = currentStats?.TotalCost ?? 0,
            SuccessRequests = currentStats?.SuccessRequests ?? 0,
            FailedRequests = currentStats?.FailedRequests ?? 0
        };
    }

    public async Task<List<UsageTrendOutputDto>> GetTrendAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var end = endTime ?? now;
        var start = startTime ?? now.AddHours(-24);
        var totalHours = (end - start).TotalHours;
        bool isDaily = totalHours > 72;

        if (isDaily) start = start.Date; // 对齐到天

        // 使用数据库分组查询，避免全量加载
        var query = await usageRecordRepository.GetQueryableAsync(cancellationToken);

        var rawData = await asyncExecuter.ToListAsync(query
            .Where(r => r.CreationTime >= start && r.CreationTime < end)
            .GroupBy(r => new { Date = r.CreationTime.Date, Hour = r.CreationTime.Hour })
            .Select(g => new TrendDataDto
            {
                Date = g.Key.Date,
                Hour = g.Key.Hour,
                Requests = g.Count(),
                InputTokens = g.Sum(r => (long)(r.InputTokens ?? 0)),
                OutputTokens = g.Sum(r => (long)(r.OutputTokens ?? 0))
            }), cancellationToken);

        return FillTimeSlots(start, end, isDaily, rawData);
    }

    public async Task<List<ApiKeyTrendOutputDto>> GetTopApiKeyTrendAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var end = endTime ?? now;
        var start = startTime ?? now.AddHours(-24);
        var totalHours = (end - start).TotalHours;
        bool isDaily = totalHours > 72;

        if (isDaily) start = start.Date;

        var query = await usageRecordRepository.GetQueryableAsync(cancellationToken);

        // 1. 获取 Top 7 API Keys
        var topKeysData = await asyncExecuter.ToListAsync(query
            .Where(r => r.CreationTime >= start && r.CreationTime < end && !string.IsNullOrEmpty(r.ApiKeyName))
            .GroupBy(r => r.ApiKeyName!)
            .Select(g => new { ApiKeyName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(7), cancellationToken);

        var topKeyNames = topKeysData.Select(x => x.ApiKeyName).ToList();

        if (!topKeyNames.Any())
        {
            return [];
        }

        // 2. 获取这些 Key 的趋势数据
        var trendData = await asyncExecuter.ToListAsync(query
            .Where(r => r.CreationTime >= start && r.CreationTime < end && topKeyNames.Contains(r.ApiKeyName!))
            .GroupBy(r => new { r.ApiKeyName, Date = r.CreationTime.Date, Hour = r.CreationTime.Hour })
            .Select(g => new { g.Key.ApiKeyName, g.Key.Date, g.Key.Hour, Count = g.Count() }), cancellationToken);

        // 3. 组装结果
        var result = new List<ApiKeyTrendOutputDto>();
        foreach (var keyName in topKeyNames)
        {
            var keyTrendData = trendData
                .Where(x => x.ApiKeyName == keyName)
                .Select(x => new TrendDataDto { Date = x.Date, Hour = x.Hour, Requests = x.Count, InputTokens = 0L, OutputTokens = 0L })
                .ToList();

            result.Add(new ApiKeyTrendOutputDto
            {
                ApiKeyName = keyName,
                TotalRequests = topKeysData.First(x => x.ApiKeyName == keyName).Count,
                Trend = FillTimeSlots(start, end, isDaily, keyTrendData)
            });
        }

        return result;
    }

    private List<UsageTrendOutputDto> FillTimeSlots(DateTime startOfPeriod, DateTime endOfPeriod, bool isDaily, List<TrendDataDto> data)
    {
        var result = new List<UsageTrendOutputDto>();
        var current = startOfPeriod;

        while (current <= endOfPeriod)
        {
            DateTime slotStart;
            int requests = 0;
            long inputTokens = 0, outputTokens = 0;

            if (isDaily)
            {
                slotStart = new DateTime(current.Year, current.Month, current.Day, 0, 0, 0, DateTimeKind.Utc);
                var items = data.Where(x => x.Date == slotStart.Date);
                requests = items.Sum(x => x.Requests);
                inputTokens = items.Sum(x => x.InputTokens);
                outputTokens = items.Sum(x => x.OutputTokens);
            }
            else
            {
                slotStart = new DateTime(current.Year, current.Month, current.Day, current.Hour, 0, 0, DateTimeKind.Utc);
                var item = data.FirstOrDefault(x => x.Date == slotStart.Date && x.Hour == slotStart.Hour);
                if (item != null)
                {
                    requests = item.Requests;
                    inputTokens = item.InputTokens;
                    outputTokens = item.OutputTokens;
                }
            }

            result.Add(new UsageTrendOutputDto
            {
                Time = slotStart.ToString("o"), // ISO 8601 标准字符串，带时区信息 (UTC)
                Requests = requests,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            });

            if (isDaily) current = current.AddDays(1);
            else current = current.AddHours(1);
        }
        return result;
    }

    public async Task<List<ModelDistributionOutputDto>> GetModelDistributionAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var end = endTime ?? now;
        var start = startTime ?? now.Date;

        var query = await usageRecordRepository.GetQueryableAsync(cancellationToken);

        var modelStats = await asyncExecuter.ToListAsync(query
            .Where(r => r.CreationTime >= start && r.CreationTime < end && !string.IsNullOrEmpty(r.DownModelId))
            .GroupBy(r => r.DownModelId)
            .Select(g => new
            {
                Model = g.Key,
                RequestCount = g.Count(),
                TotalTokens = g.Sum(r => (long)(r.InputTokens ?? 0) + (long)(r.OutputTokens ?? 0)),
                TotalCost = g.Sum(r => r.FinalCost)
            })
            .OrderByDescending(x => x.RequestCount)
            .Take(7), cancellationToken);

        var totalRequests = modelStats.Sum(x => x.RequestCount);

        return modelStats.Select(x => new ModelDistributionOutputDto
        {
            Model = x.Model ?? "Unknown",
            RequestCount = x.RequestCount,
            TotalTokens = x.TotalTokens,
            TotalCost = x.TotalCost ?? 0,
            Percentage = totalRequests > 0 ? Math.Round((decimal)x.RequestCount / totalRequests * 100, 2) : 0
        }).ToList();
    }
}

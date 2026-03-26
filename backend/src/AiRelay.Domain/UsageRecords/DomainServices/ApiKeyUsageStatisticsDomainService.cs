using AiRelay.Domain.ApiKeys.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.UsageRecords.DomainServices;

/// <summary>
/// ApiKey 统计领域服务
/// </summary>
public class ApiKeyUsageStatisticsDomainService(
    IRepository<ApiKey, Guid> apiKeyRepository,
    IQueryableAsyncExecuter asyncExecuter)
{
    /// <summary>
    /// 获取全局聚合指标
    /// </summary>
    public async Task<(
        long TotalSubscriptions,
        long ActiveSubscriptions,
        long ExpiringSoon,
        long TotalUsageToday,
        decimal UsageGrowthRate,
        List<(string Name, long Usage)> TopUsageKeys
    )> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var yesterday = today.AddDays(-1);
        var next7Days = now.AddDays(7);

        var query = await apiKeyRepository.GetQueryableAsync(cancellationToken);

        // 单次条件聚合：订阅数、活跃数、即将过期数、今日用量、昨日用量
        var stats = await asyncExecuter.FirstOrDefaultAsync(query
            .GroupBy(k => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(k => k.IsActive),
                ExpiringSoon = g.Count(k =>
                    k.ExpiresAt.HasValue &&
                    k.ExpiresAt.Value > now &&
                    k.ExpiresAt.Value < next7Days),
                // UsageToday 仅在 StatsDate 为今日时有效，否则视为 0
                TotalUsageToday = g.Sum(k => k.StatsDate != null && k.StatsDate.Value.Date == today ? k.UsageToday : 0),
                TotalUsageYesterday = g.Sum(k => k.StatsDate != null && k.StatsDate.Value.Date == yesterday ? k.UsageToday : 0)
            }), cancellationToken);

        var totalUsageToday = stats?.TotalUsageToday ?? 0L;
        var totalUsageYesterday = stats?.TotalUsageYesterday ?? 0L;

        decimal growthRate = totalUsageYesterday > 0
            ? Math.Round((decimal)(totalUsageToday - totalUsageYesterday) / totalUsageYesterday * 100, 2)
            : totalUsageToday > 0 ? 100m : 0m;

        // Top 5 ApiKeys by today's usage
        var topUsage = await asyncExecuter.ToListAsync(query
            .Where(k => k.StatsDate != null && k.StatsDate.Value.Date == today && k.UsageToday > 0)
            .OrderByDescending(k => k.UsageToday)
            .Take(5)
            .Select(k => new { k.Name, k.UsageToday }), cancellationToken);

        return (
            stats?.Total ?? 0,
            stats?.Active ?? 0,
            stats?.ExpiringSoon ?? 0,
            totalUsageToday,
            growthRate,
            topUsage.Select(x => (x.Name, (long)x.UsageToday)).ToList()
        );
    }
}

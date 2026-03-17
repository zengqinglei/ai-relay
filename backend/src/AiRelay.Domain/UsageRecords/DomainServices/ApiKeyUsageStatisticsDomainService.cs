using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.UsageRecords.DomainServices;

/// <summary>
/// ApiKey 统计领域服务
/// </summary>
public class ApiKeyUsageStatisticsDomainService(
    IRepository<ApiKey, Guid> apiKeyRepository,
    IRepository<UsageRecord, Guid> usageRecordRepository,
    IQueryableAsyncExecuter asyncExecuter)
{
    /// <summary>
    /// 获取 ApiKey 列表统计数据
    /// </summary>
    public async Task<Dictionary<Guid, (long UsageToday, long UsageTotal)>> GetListStatisticsAsync(
        List<Guid> apiKeyIds,
        CancellationToken cancellationToken = default)
    {
        if (apiKeyIds == null || apiKeyIds.Count == 0)
        {
            return [];
        }

        var today = DateTime.UtcNow.Date;
        var query = await usageRecordRepository.GetQueryableAsync();

        // 1. 获取总调用量
        var totalUsage = await asyncExecuter.ToListAsync(query
            .Where(x => apiKeyIds.Contains(x.ApiKeyId))
            .GroupBy(x => x.ApiKeyId)
            .Select(g => new { ApiKeyId = g.Key, Count = g.Count() }), cancellationToken);

        // 2. 获取今日调用量
        var todayUsage = await asyncExecuter.ToListAsync(query
            .Where(x => apiKeyIds.Contains(x.ApiKeyId) && x.CreationTime >= today)
            .GroupBy(x => x.ApiKeyId)
            .Select(g => new { ApiKeyId = g.Key, Count = g.Count() }), cancellationToken);

        // 3. 组装结果
        var result = new Dictionary<Guid, (long UsageToday, long UsageTotal)>();
        foreach (var id in apiKeyIds)
        {
            // 注意：Anonymous type 的属性访问通常是安全的，但如果跨程序集可能会有问题。
            // 只要 IQueryableAsyncExecuter 泛型能推断正确即可。
            var total = totalUsage.FirstOrDefault(x => x.ApiKeyId == id)?.Count ?? 0;
            var todayCount = todayUsage.FirstOrDefault(x => x.ApiKeyId == id)?.Count ?? 0;
            result[id] = (todayCount, total);
        }

        return result;
    }

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
        // 顺序执行查询以避免 DbContext 并发冲突
        var total = await apiKeyRepository.CountAsync(x => true, cancellationToken);
        var active = await apiKeyRepository.CountAsync(k => k.IsActive, cancellationToken);

        var next7Days = DateTime.UtcNow.AddDays(7);
        var now = DateTime.UtcNow;
        var expiringSoon = await apiKeyRepository.CountAsync(k =>
            k.ExpiresAt.HasValue &&
            k.ExpiresAt.Value > now &&
            k.ExpiresAt.Value < next7Days, cancellationToken);

        // 2. 用量统计 - 暂时模拟，因为 UsageRecord 尚未明确关联 ApiKey
        long totalUsageToday = 0;
        decimal growthRate = 0;
        var topUsage = new List<(string Name, long Usage)>();

        return (total, active, expiringSoon, totalUsageToday, growthRate, topUsage);
    }
}

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

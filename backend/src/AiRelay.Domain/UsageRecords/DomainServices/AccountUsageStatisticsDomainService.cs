using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.UsageRecords.DomainServices;

/// <summary>
/// 账户统计领域服务
/// </summary>
public class AccountUsageStatisticsDomainService(
    IRepository<AccountToken, Guid> accountTokenRepository,
    IRepository<UsageRecord, Guid> usageRecordRepository)
{
    /// <summary>
    /// 获取账户列表统计数据（优化：使用单次分组查询）
    /// </summary>
    public async Task<Dictionary<Guid, (long UsageToday, long UsageTotal, decimal SuccessRate)>> GetListStatisticsAsync(
        List<Guid> accountIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, (long UsageToday, long UsageTotal, decimal SuccessRate)>();

        if (accountIds == null || !accountIds.Any())
        {
            return result;
        }

        var today = DateTime.UtcNow.Date;

        // 获取所有相关记录（使用索引：AccountTokenId + CreationTime）
        var records = await usageRecordRepository.GetListAsync(
            u => accountIds.Contains(u.AccountTokenId),
            cancellationToken: cancellationToken);

        // 内存分组统计（记录数量通常不大，分页场景下每个账户平均几百到几千条）
        var grouped = records.GroupBy(r => r.AccountTokenId);

        foreach (var group in grouped)
        {
            var accountId = group.Key;
            var allRecords = group.ToList();

            var total = allRecords.Count;
            var todayCount = allRecords.Count(r => r.CreationTime >= today);
            var successCount = allRecords.Count(r => r.UpStatusCode >= 200 && r.UpStatusCode < 300);

            decimal rate = total > 0 ? Math.Round((decimal)successCount / total * 100, 2) : 0m;

            result[accountId] = (todayCount, total, rate);
        }

        // 补充没有记录的账户
        foreach (var id in accountIds)
        {
            if (!result.ContainsKey(id))
            {
                result[id] = (0, 0, 0m);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取全局聚合指标
    /// </summary>
    public async Task<(
        int TotalAccounts,
        int ActiveAccounts,
        int DisabledAccounts,
        int ExpiringAccounts,
        long TotalUsageToday,
        decimal UsageGrowthRate,
        decimal AverageSuccessRate,
        long AbnormalRequests24h
    )> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        // 1. 账户状态统计 - 数据量较小，可以使用 GetListAsync 后内存统计
        var accounts = await accountTokenRepository.GetListAsync(cancellationToken: cancellationToken);

        var totalAccounts = accounts.Count();
        var activeAccounts = accounts.Count(a => a.IsActive);
        var disabledAccounts = accounts.Count(a => !a.IsActive);

        // 统计需要轮换的账户：已过期或即将过期（24小时内），但排除有 RefreshToken 的账户（会自动刷新）
        var expiringAccounts = accounts.Count(a =>
        {
            // 如果有 RefreshToken，系统会自动刷新，不需要人工轮换
            if (!string.IsNullOrEmpty(a.RefreshToken))
            {
                return false;
            }

            var remaining = a.GetTokenRemainingMinutes();
            // 包含已过期（remaining <= 0）和即将过期（0 < remaining < 1440分钟）的账户
            return remaining.HasValue && remaining.Value < 1440; // 24小时
        });

        // 2. 使用量统计 - 顺序执行查询以避免 DbContext 并发冲突
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var last24Hours = DateTime.UtcNow.AddHours(-24);

        var todayUsage = await usageRecordRepository.CountAsync(u => u.CreationTime >= today, cancellationToken);

        var yesterdayUsage = await usageRecordRepository.CountAsync(
            u => u.CreationTime >= yesterday && u.CreationTime < today,
            cancellationToken);

        var totalRequests = await usageRecordRepository.CountAsync(x => true, cancellationToken);

        var successRequests = await usageRecordRepository.CountAsync(
            u => u.UpStatusCode >= 200 && u.UpStatusCode < 300,
            cancellationToken);

        var abnormalRequests = await usageRecordRepository.CountAsync(
            u => u.CreationTime >= last24Hours && (u.UpStatusCode < 200 || u.UpStatusCode >= 300),
            cancellationToken);

        var growthRate = yesterdayUsage > 0
            ? Math.Round((decimal)(todayUsage - yesterdayUsage) / yesterdayUsage * 100, 2)
            : 0m;

        var averageSuccessRate = totalRequests > 0
            ? Math.Round((decimal)successRequests / totalRequests * 100, 2)
            : 0m;

        return (
            totalAccounts,
            activeAccounts,
            disabledAccounts,
            expiringAccounts,
            (long)todayUsage,
            growthRate,
            averageSuccessRate,
            abnormalRequests
        );
    }
}

using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.UsageRecords.DomainServices;

/// <summary>
/// 账户统计领域服务
/// </summary>
public class AccountUsageStatisticsDomainService(
    IRepository<AccountToken, Guid> accountTokenRepository,
    IRepository<UsageRecord, Guid> usageRecordRepository,
    IQueryableAsyncExecuter asyncExecuter)
{
    /// <summary>
    /// 获取全局聚合指标
    /// </summary>
    public async Task<(
        int TotalAccounts,
        int ActiveAccounts,
        int DisabledAccounts,
        int ExpiringAccounts,
        long TotalUsageToday,
        long TotalInputTokensToday,
        long TotalOutputTokensToday,
        decimal UsageGrowthRate,
        decimal AverageSuccessRate,
        long SuccessfulRequests24h,
        long TotalRequests24h,
        long AbnormalRequests24h,
        int RotationWarnings
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
        var rotationWarnings = accounts.Count(a => a.GetEffectiveStatus() != AccountStatus.Normal);

        // 2. 使用量统计 - 单次条件聚合查询
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var last24Hours = DateTime.UtcNow.AddHours(-24);

        var query = await usageRecordRepository.GetQueryableAsync(cancellationToken);
        var usageStats = await asyncExecuter.SingleOrDefaultAsync(query
            .GroupBy(r => 1)
            .Select(g => new
            {
                TodayUsage = g.Count(r => r.CreationTime >= today),
                TodayInputTokens = g.Sum(r => r.CreationTime >= today ? (long)(r.InputTokens ?? 0) : 0),
                TodayOutputTokens = g.Sum(r => r.CreationTime >= today ? (long)(r.OutputTokens ?? 0) : 0),
                YesterdayUsage = g.Count(r => r.CreationTime >= yesterday && r.CreationTime < today),
                TotalRequests = g.Count(),
                SuccessRequests = g.Count(r => r.Status == UsageStatus.Success),
                SuccessfulRequests24h = g.Count(r => r.CreationTime >= last24Hours && r.Status == UsageStatus.Success),
                TotalRequests24h = g.Count(r => r.CreationTime >= last24Hours),
                AbnormalRequests = g.Count(r => r.CreationTime >= last24Hours && r.Status == UsageStatus.Failed)
            }), cancellationToken);

        var todayUsage = usageStats?.TodayUsage ?? 0;
        var todayInputTokens = usageStats?.TodayInputTokens ?? 0;
        var todayOutputTokens = usageStats?.TodayOutputTokens ?? 0;
        var yesterdayUsage = usageStats?.YesterdayUsage ?? 0;
        var totalRequests = usageStats?.TotalRequests ?? 0;
        var successRequests = usageStats?.SuccessRequests ?? 0;
        var successfulRequests24h = usageStats?.SuccessfulRequests24h ?? 0;
        var totalRequests24h = usageStats?.TotalRequests24h ?? 0;
        var abnormalRequests = usageStats?.AbnormalRequests ?? 0;

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
            todayUsage,
            todayInputTokens,
            todayOutputTokens,
            growthRate,
            averageSuccessRate,
            successfulRequests24h,
            totalRequests24h,
            abnormalRequests,
            rotationWarnings
        );
    }
}

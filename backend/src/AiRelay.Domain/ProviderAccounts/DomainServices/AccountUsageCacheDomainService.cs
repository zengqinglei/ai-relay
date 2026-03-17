using Microsoft.Extensions.Caching.Distributed;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账户使用计数缓存领域服务
/// </summary>
public class AccountUsageCacheDomainService(IDistributedCache cache)
{
    private const string CacheKeyPrefix = "account:usage:";
    private const string BackoffCountKeyPrefix = "account:backoff:";

    /// <summary>
    /// 原子递增退避计数，返回递增后的新值。
    /// 注意：IDistributedCache 不支持原子 INCR，此处通过"先读后写"实现，
    /// 调用方应在已持有熔断锁（IsRateLimited 检查通过后）的语义下调用，以避免并发竞态。
    /// 初始 TTL 使用最大熔断时长兜底，调用方应随后调用 RefreshBackoffCountTtlAsync 设置精确 TTL。
    /// </summary>
    public async Task<int> IncrementBackoffCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default)
    {
        var key = $"{BackoffCountKeyPrefix}{accountTokenId}";
        var currentStr = await cache.GetStringAsync(key, cancellationToken);
        var count = string.IsNullOrEmpty(currentStr) ? 0 : int.Parse(currentStr);
        var newCount = count + 1;

        await cache.SetStringAsync(key, newCount.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(3600) // 最大熔断时长兜底
        }, cancellationToken);

        return newCount;
    }

    /// <summary>
    /// 刷新退避计数的 TTL，使其与熔断时长对齐。
    /// 熔断解除后计数自动过期，下次失败重新从第一级退避开始，避免跨熔断周期的计数累积。
    /// </summary>
    public async Task RefreshBackoffCountTtlAsync(Guid accountTokenId, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var key = $"{BackoffCountKeyPrefix}{accountTokenId}";
        var currentStr = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(currentStr)) return;

        await cache.SetStringAsync(key, currentStr, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lockDuration
        }, cancellationToken);
    }

    public async Task ClearBackoffCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default)
    {
        var key = $"{BackoffCountKeyPrefix}{accountTokenId}";
        await cache.RemoveAsync(key, cancellationToken);
    }

    public async Task<long> IncrementUsageAsync(Guid accountTokenId, ProviderPlatform platform, string accountName, CancellationToken cancellationToken = default)
    {
        var key = $"{CacheKeyPrefix}{accountTokenId}";

        var currentStr = await cache.GetStringAsync(key, cancellationToken);
        var current = string.IsNullOrEmpty(currentStr) ? 0L : long.Parse(currentStr);
        var newValue = current + 1;

        var expiration = GetNextResetTime(platform);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiration
        };

        await cache.SetStringAsync(key, newValue.ToString(), options, cancellationToken);

        return newValue;
    }

    public async Task<Dictionary<Guid, long>> GetUsageCountsAsync(IEnumerable<Guid> accountTokenIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, long>();

        foreach (var id in accountTokenIds)
        {
            var key = $"{CacheKeyPrefix}{id}";
            var valueStr = await cache.GetStringAsync(key, cancellationToken);
            result[id] = string.IsNullOrEmpty(valueStr) ? 0L : long.Parse(valueStr);
        }

        return result;
    }

    private static DateTimeOffset GetNextResetTime(ProviderPlatform platform)
    {
        var resetHour = platform switch
        {
            ProviderPlatform.GEMINI_OAUTH or ProviderPlatform.GEMINI_APIKEY => 15,
            _ => 0
        };

        var now = DateTime.UtcNow;
        var todayReset = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0, DateTimeKind.Utc);

        return now < todayReset ? todayReset : todayReset.AddDays(1);
    }
}

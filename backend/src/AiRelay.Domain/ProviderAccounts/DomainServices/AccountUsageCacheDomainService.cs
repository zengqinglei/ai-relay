using Microsoft.Extensions.Caching.Distributed;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账户退避计数缓存领域服务
/// </summary>
public class AccountUsageCacheDomainService(IDistributedCache cache)
{
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

}

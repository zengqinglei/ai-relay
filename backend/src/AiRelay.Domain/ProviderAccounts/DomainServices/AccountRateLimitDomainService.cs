using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Domain.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账户限流领域服务（Redis TTL + 数据库持久化 + 三级降级策略）
/// </summary>
public class AccountRateLimitDomainService(
    IDistributedCache cache,
    IRepository<AccountToken, Guid> accountRepository,
    ILogger<AccountRateLimitDomainService> logger)
{
    private const int BACKOFF_BASE_SECONDS = 5;  // 指数退避基础值
    private const int BACKOFF_MAX_SECONDS = 3600;  // 指数退避最大值（1小时）

    /// <summary>
    /// 锁定账号：仅写入 Redis TTL，不操作数据库。
    /// 数据库持久化由调用方（HandleFailureAsync）统一完成，避免重复 DB 写入。
    /// </summary>
    public async Task LockAsync(Guid accountId, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var lockDurationSeconds = (int)Math.Ceiling(lockDuration.TotalSeconds);
        lockDurationSeconds = Math.Max(BACKOFF_BASE_SECONDS, Math.Min(lockDurationSeconds, BACKOFF_MAX_SECONDS));

        var key = $"RateLimit:{accountId}";
        var lockInfo = new RateLimitLockInfo
        {
            LockedAt = DateTime.UtcNow,
            UnlockAt = DateTime.UtcNow.AddSeconds(lockDurationSeconds),
            Reason = "Rate limited by upstream API"
        };

        await cache.SetStringAsync(key, JsonSerializer.Serialize(lockInfo), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(lockDurationSeconds)
        }, cancellationToken);
    }

    /// <summary>
    /// 检查账号是否在限流期：快速路径 Redis + 慢速路径数据库
    /// </summary>
    public async Task<bool> IsRateLimitedAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var key = $"RateLimit:{accountId}";

        // 快速路径: 检查 Redis
        var cachedValue = await cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cachedValue))
        {
            try
            {
                var lockInfo = JsonSerializer.Deserialize<RateLimitLockInfo>(cachedValue);
                if (lockInfo != null && lockInfo.UnlockAt > DateTime.UtcNow)
                {
                    logger.LogDebug("账号 {AccountId} 仍在限流期 (Redis)，解锁时间: {UnlockAt}",
                        accountId, lockInfo.UnlockAt);
                    return true;
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "解析 Redis 限流信息失败，回退到数据库检查");
            }
        }

        // 慢速路径: 检查数据库 (Redis 缺失或过期时)
        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account != null && account.Status == AccountStatus.RateLimited)
        {
            // 检查数据库中的 LockedUntil 是否已过期
            if (account.LockedUntil.HasValue && account.LockedUntil.Value > DateTime.UtcNow)
            {
                logger.LogDebug("账号 {AccountId} 仍在限流期 (数据库)，解锁时间: {LockedUntil}",
                    accountId, account.LockedUntil.Value);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 批量检查账号限流状态（性能优化）
    /// </summary>
    public async Task<HashSet<Guid>> GetRateLimitedAccountIdsAsync(
        IEnumerable<Guid> accountIds,
        CancellationToken cancellationToken = default)
    {
        var ids = accountIds.ToList();
        if (ids.Count == 0) return new HashSet<Guid>();

        var keys = ids.Select(id => $"RateLimit:{id}").ToArray();
        var rateLimitedIds = new HashSet<Guid>();

        // 批量查询 Redis
        var tasks = keys.Select(key => cache.GetStringAsync(key, cancellationToken));
        var results = await Task.WhenAll(tasks);

        for (int i = 0; i < results.Length; i++)
        {
            if (string.IsNullOrEmpty(results[i])) continue;

            try
            {
                var lockInfo = JsonSerializer.Deserialize<RateLimitLockInfo>(results[i]!);
                if (lockInfo != null && lockInfo.UnlockAt > DateTime.UtcNow)
                {
                    rateLimitedIds.Add(ids[i]);
                }
            }
            catch (JsonException)
            {
                // 解析失败，跳过
            }
        }

        return rateLimitedIds;
    }

    /// <summary>
    /// 强制清除限流状态（管理端干预）
    /// </summary>
    public async Task ClearAsync(AccountToken account, CancellationToken cancellationToken = default)
    {
        var key = $"RateLimit:{account.Id}";
        var successKey = $"RateLimit:Success:{account.Id}";

        // 1. 移除 Redis 中的 Key
        await cache.RemoveAsync(key, cancellationToken);
        await cache.RemoveAsync(successKey, cancellationToken);

        // 2. 重置数据库中的状态（支持限流和异常状态）
        if (account.ResetStatus())
        {
            await accountRepository.UpdateAsync(account, cancellationToken);
            logger.LogInformation("账号 '{AccountName}' 状态已重置: {OldStatus} -> Normal", account.Name, account.Status);
        }
    }

}

/// <summary>
/// 限流锁定信息
/// </summary>
public record RateLimitLockInfo
{
    public DateTime LockedAt { get; init; }
    public DateTime UnlockAt { get; init; }
    public string Reason { get; init; } = string.Empty;
}

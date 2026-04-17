using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Domain.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账户限流领域服务（Redis TTL + 数据库持久化 + 账户级/模型级退避控制）
/// </summary>
public class AccountRateLimitDomainService(
    IDistributedCache cache,
    IRepository<AccountToken, Guid> accountRepository,
    ILogger<AccountRateLimitDomainService> logger)
{
    private const int BACKOFF_BASE_SECONDS = 5;      // 指数退避基础值
    private const int BACKOFF_MAX_SECONDS = 216000;  // 指数退避最大值（2.5天）

    private const string BackoffCountKeyPrefix = "account:backoff:";
    private const string ModelBackoffCountKeyPrefix = "account:model:backoff:";

    // ============ 限流 ============

    /// <summary>
    /// 锁定账号：仅写入 Redis TTL，不操作数据库。
    /// 数据库持久化由调用方统一完成，避免重复 DB 写入。
    /// </summary>
    public async Task LockAsync(Guid accountId, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var lockDurationSeconds = NormalizeLockDurationSeconds(lockDuration);
        var key = GetAccountRateLimitKey(accountId);
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
    /// 锁定账号下的某个具体模型。
    /// 仅在按模型限流模式下使用。
    /// </summary>
    public async Task LockModelAsync(Guid accountId, string modelKey, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return;
        }

        var lockDurationSeconds = NormalizeLockDurationSeconds(lockDuration);
        var key = GetModelRateLimitKey(accountId, modelKey);
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
        var cached = await cache.GetStringAsync(GetAccountRateLimitKey(accountId), cancellationToken);
        if (TryReadActiveLock(cached, out var unlockAt))
        {
            logger.LogDebug("账号 {AccountId} 仍在限流期 (Redis)，解锁时间: {UnlockAt}", accountId, unlockAt);
            return true;
        }

        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account != null && account.Status == AccountStatus.RateLimited && account.LockedUntil.HasValue && account.LockedUntil.Value > DateTime.UtcNow)
        {
            logger.LogDebug("账号 {AccountId} 仍在限流期 (数据库)，解锁时间: {LockedUntil}", accountId, account.LockedUntil.Value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查账号下某个模型是否在模型级限流期。
    /// </summary>
    public async Task<bool> IsModelRateLimitedAsync(Guid accountId, string modelKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return false;
        }

        var cached = await cache.GetStringAsync(GetModelRateLimitKey(accountId, modelKey), cancellationToken);
        if (TryReadActiveLock(cached, out _))
        {
            return true;
        }

        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        return account?.IsModelRateLimited(modelKey) == true;
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

        var results = await Task.WhenAll(ids.Select(id => cache.GetStringAsync(GetAccountRateLimitKey(id), cancellationToken)));
        var rateLimitedIds = new HashSet<Guid>();

        for (int i = 0; i < results.Length; i++)
        {
            if (TryReadActiveLock(results[i], out _))
            {
                rateLimitedIds.Add(ids[i]);
            }
        }

        return rateLimitedIds;
    }

    /// <summary>
    /// 强制清除限流状态（管理端干预）
    /// </summary>
    public async Task ClearAsync(AccountToken account, CancellationToken cancellationToken = default)
    {
        // 1. 移除 Redis 中的所有相关 Key
        await cache.RemoveAsync(GetAccountRateLimitKey(account.Id), cancellationToken);
        await cache.RemoveAsync($"RateLimit:Success:{account.Id}", cancellationToken);
        await cache.RemoveAsync(GetBackoffCountKey(account.Id), cancellationToken);

        foreach (var limitedModel in account.GetActiveLimitedModels())
        {
            await cache.RemoveAsync(GetModelRateLimitKey(account.Id, limitedModel.ModelKey), cancellationToken);
            await cache.RemoveAsync(GetModelBackoffCountKey(account.Id, limitedModel.ModelKey), cancellationToken);
        }

        // 2. 重置数据库中的状态（支持限流、部分限流和异常状态）
        if (account.ResetStatus())
        {
            await accountRepository.UpdateAsync(account, cancellationToken);
            logger.LogInformation("账号 '{AccountName}' 状态已重置", account.Name);
        }
    }

    /// <summary>
    /// 清除某个模型的模型级熔断锁与退避计数。
    /// </summary>
    public async Task ClearModelLockAsync(Guid accountId, string? modelKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return;
        }

        await cache.RemoveAsync(GetModelRateLimitKey(accountId, modelKey), cancellationToken);
        await cache.RemoveAsync(GetModelBackoffCountKey(accountId, modelKey), cancellationToken);
    }

    // ============ 退避计数 ============

    /// <summary>
    /// 原子递增账号级退避计数，返回递增后的新值。
    /// 注意：IDistributedCache 不支持原子 INCR，此处通过“先读后写”实现。
    /// </summary>
    public Task<int> IncrementBackoffCountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return IncrementBackoffCountInternalAsync(GetBackoffCountKey(accountId), cancellationToken);
    }

    /// <summary>
    /// 原子递增模型级退避计数，返回递增后的新值。
    /// </summary>
    public Task<int> IncrementModelBackoffCountAsync(Guid accountId, string modelKey, CancellationToken cancellationToken = default)
    {
        return IncrementBackoffCountInternalAsync(GetModelBackoffCountKey(accountId, modelKey), cancellationToken);
    }

    /// <summary>
    /// 获取账号级退避计数。
    /// </summary>
    public Task<int> GetBackoffCountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return GetBackoffCountInternalAsync(GetBackoffCountKey(accountId), cancellationToken);
    }

    /// <summary>
    /// 获取模型级退避计数。
    /// </summary>
    public Task<int> GetModelBackoffCountAsync(Guid accountId, string modelKey, CancellationToken cancellationToken = default)
    {
        return GetBackoffCountInternalAsync(GetModelBackoffCountKey(accountId, modelKey), cancellationToken);
    }

    /// <summary>
    /// 清除账号级退避计数。
    /// </summary>
    public Task ClearBackoffCountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return cache.RemoveAsync(GetBackoffCountKey(accountId), cancellationToken);
    }

    /// <summary>
    /// 清除模型级退避计数。
    /// </summary>
    public Task ClearModelBackoffCountAsync(Guid accountId, string modelKey, CancellationToken cancellationToken = default)
    {
        return cache.RemoveAsync(GetModelBackoffCountKey(accountId, modelKey), cancellationToken);
    }

    private async Task<int> IncrementBackoffCountInternalAsync(string key, CancellationToken cancellationToken)
    {
        var currentStr = await cache.GetStringAsync(key, cancellationToken);
        var count = string.IsNullOrEmpty(currentStr) ? 0 : int.Parse(currentStr);
        var newCount = count + 1;

        await cache.SetStringAsync(key, newCount.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(BACKOFF_MAX_SECONDS)
        }, cancellationToken);

        return newCount;
    }

    private async Task<int> GetBackoffCountInternalAsync(string key, CancellationToken cancellationToken)
    {
        var currentStr = await cache.GetStringAsync(key, cancellationToken);
        return string.IsNullOrEmpty(currentStr) ? 0 : int.Parse(currentStr);
    }

    private static int NormalizeLockDurationSeconds(TimeSpan lockDuration)
    {
        var seconds = (int)Math.Ceiling(lockDuration.TotalSeconds);
        return Math.Max(BACKOFF_BASE_SECONDS, Math.Min(seconds, BACKOFF_MAX_SECONDS));
    }

    private static bool TryReadActiveLock(string? cachedValue, out DateTime unlockAt)
    {
        unlockAt = default;
        if (string.IsNullOrEmpty(cachedValue))
        {
            return false;
        }

        try
        {
            var lockInfo = JsonSerializer.Deserialize<RateLimitLockInfo>(cachedValue);
            if (lockInfo != null && lockInfo.UnlockAt > DateTime.UtcNow)
            {
                unlockAt = lockInfo.UnlockAt;
                return true;
            }
        }
        catch (JsonException)
        {
            // 解析失败时忽略，回退数据库或视为无缓存命中
        }

        return false;
    }

    private static string GetAccountRateLimitKey(Guid accountId) => $"RateLimit:{accountId}";

    private static string GetModelRateLimitKey(Guid accountId, string modelKey) => $"RateLimit:{accountId}:{modelKey}";

    private static string GetBackoffCountKey(Guid accountId) => $"{BackoffCountKeyPrefix}{accountId}";

    private static string GetModelBackoffCountKey(Guid accountId, string modelKey) => $"{ModelBackoffCountKeyPrefix}{accountId}:{modelKey}";
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

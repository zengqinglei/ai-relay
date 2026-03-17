using Leistd.Lock.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leistd.Lock.Redis;

/// <summary>
/// Redis 分布式锁实现
/// 使用 StackExchange.Redis 的 LockTake / LockRelease API（内部封装 SET NX + Lua 脚本）
/// </summary>
public sealed class RedisDistributedLock(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisDistributedLock> logger)
    : IDistributedLock
{
    private static readonly TimeSpan DefaultLockExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    public async Task<ILockHandle> LockAsync(string key, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = await TryAcquireAsync(key, cancellationToken);
            if (handle != null)
                return handle;
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public async Task<ILockHandle?> TryLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = await TryAcquireAsync(key, cancellationToken);
            if (handle != null)
                return handle;
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(PollInterval < remaining ? PollInterval : remaining, cancellationToken);
        }
        logger.LogDebug("尝试加锁【{Key}】失败：超时", key);
        return null;
    }

    public async Task UnlockAsync(string key, CancellationToken cancellationToken = default)
    {
        // 显式解锁不持有 token，直接删除（用于异常兜底场景）
        var db = connectionMultiplexer.GetDatabase();
        await db.KeyDeleteAsync(key);
        logger.LogDebug("解锁【{Key}】", key);
    }

    private async Task<ILockHandle?> TryAcquireAsync(string key, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var db = connectionMultiplexer.GetDatabase();

        // 使用 LockTake API（内部封装 SET NX + 过期时间）
        var acquired = await db.LockTakeAsync(key, token, DefaultLockExpiry);
        if (!acquired)
            return null;

        logger.LogDebug("加锁【{Key}】成功", key);
        return new RedisLockHandle(key, token, this);
    }

    internal async Task ReleaseAsync(string key, string token)
    {
        var db = connectionMultiplexer.GetDatabase();

        // 使用 LockRelease API（内部封装 Lua 脚本原子校验 + 删除）
        var released = await db.LockReleaseAsync(key, token);
        if (released)
            logger.LogDebug("解锁【{Key}】成功", key);
        else
            logger.LogWarning("解锁【{Key}】失败：锁已过期或被他人持有", key);
    }
}

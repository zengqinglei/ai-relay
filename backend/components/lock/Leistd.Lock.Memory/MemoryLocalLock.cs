using Leistd.Lock.Core;
using Leistd.Lock.Memory.Entry;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Leistd.Lock.Memory;

/// <summary>
/// 内存本地锁实现
/// 使用 SemaphoreSlim(1,1) per key，适用于单机/测试场景
/// </summary>
public sealed class MemoryLocalLock(ILogger<MemoryLocalLock> logger) : ILocalLock, IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreEntry> _semaphores = new();

    internal ConcurrentDictionary<string, SemaphoreEntry> Semaphores => _semaphores;

    public async Task<ILockHandle> LockAsync(string key, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("开始加锁【{Key}】...", key);
        var entry = _semaphores.GetOrAdd(key, _ => new SemaphoreEntry());
        await entry.Semaphore.WaitAsync(cancellationToken);
        logger.LogTrace("加锁【{Key}】成功", key);
        return new MemoryLockHandle(key, this);
    }

    public async Task<ILockHandle?> TryLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("开始尝试加锁【{Key}】...", key);
        var entry = _semaphores.GetOrAdd(key, _ => new SemaphoreEntry());
        var acquired = await entry.Semaphore.WaitAsync(timeout, cancellationToken);
        if (!acquired)
        {
            logger.LogDebug("尝试加锁【{Key}】失败：超时", key);
            return null;
        }
        logger.LogTrace("尝试加锁【{Key}】成功", key);
        return new MemoryLockHandle(key, this);
    }

    public Task UnlockAsync(string key, CancellationToken cancellationToken = default)
    {
        Release(key);
        return Task.CompletedTask;
    }

    internal void Release(string key)
    {
        if (_semaphores.TryGetValue(key, out var entry))
        {
            entry.Semaphore.Release();
            entry.LastReleasedAt = DateTime.UtcNow;
            logger.LogTrace("解锁【{Key}】成功", key);
        }
        else
        {
            logger.LogWarning("解锁【{Key}】失败：未找到对应信号量", key);
        }
    }

    public void Dispose()
    {
        foreach (var entry in _semaphores.Values)
            entry.Semaphore.Dispose();
        _semaphores.Clear();
    }


}

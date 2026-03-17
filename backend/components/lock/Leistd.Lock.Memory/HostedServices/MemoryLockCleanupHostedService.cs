using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leistd.Lock.Memory.HostedServices;

/// <summary>
/// 内存锁清理后台服务
/// 定期清理长时间空闲的 Semaphore 资源，防止内存泄漏
/// </summary>
public sealed class MemoryLockCleanupHostedService(
    MemoryLocalLock memoryLock,
    ILogger<MemoryLockCleanupHostedService> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxIdleTime = TimeSpan.FromMinutes(5);
    private Timer? _cleanupTimer;
    private CancellationTokenSource? _stoppingCts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = new CancellationTokenSource();
        _cleanupTimer = new Timer(_ => Cleanup(), null, CleanupInterval, CleanupInterval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingCts?.Cancel();
        _cleanupTimer?.Change(Timeout.Infinite, 0);
        _cleanupTimer?.Dispose();
        return Task.CompletedTask;
    }

    private void Cleanup()
    {
        if (_stoppingCts?.Token.IsCancellationRequested == true) return;

        logger.LogTrace("开始清理超时 Semaphore 锁...");
        var now = DateTime.UtcNow;
        foreach (var (key, entry) in memoryLock.Semaphores)
        {
            if (_stoppingCts?.Token.IsCancellationRequested == true) break;

            if (entry.Semaphore.CurrentCount == 1 && now - entry.LastReleasedAt > MaxIdleTime)
            {
                if (memoryLock.Semaphores.TryRemove(key, out _))
                    logger.LogTrace("清理超时 Semaphore 锁【{Key}】", key);
            }
        }
        logger.LogTrace("清理超时 Semaphore 锁完成");
    }

    public void Dispose()
    {
        _stoppingCts?.Dispose();
        _cleanupTimer?.Dispose();
    }
}

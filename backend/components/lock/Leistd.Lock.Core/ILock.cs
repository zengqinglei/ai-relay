namespace Leistd.Lock.Core;

/// <summary>
/// 锁服务统一接口（对应 Java IDistributedLock / ILocalLock）
/// 实现可以是分布式锁（Redis）或本地锁（Memory），由 DI 注册决定
/// </summary>
public interface ILock
{
    /// <summary>
    /// 阻塞加锁，直到获取成功（对应 Java lock(key)）
    /// </summary>
    Task<ILockHandle> LockAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试加锁，超时后返回 null（对应 Java lock(key, time, unit)）
    /// 返回 null 表示锁被占用，非异常情况，调用方自行处理降级逻辑
    /// </summary>
    Task<ILockHandle?> TryLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 显式解锁（对应 Java unlock(key)）
    /// 通常通过 ILockHandle.DisposeAsync 自动调用，无需手动调用
    /// </summary>
    Task UnlockAsync(string key, CancellationToken cancellationToken = default);
}

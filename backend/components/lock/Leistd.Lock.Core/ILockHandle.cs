namespace Leistd.Lock.Core;

/// <summary>
/// 锁句柄，使用完毕后释放锁（对应 Java ILock AutoCloseable）
/// </summary>
public interface ILockHandle : IAsyncDisposable
{
}

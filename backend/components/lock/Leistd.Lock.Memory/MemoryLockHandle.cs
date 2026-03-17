using Leistd.Lock.Core;

namespace Leistd.Lock.Memory;

/// <summary>
/// 内存锁句柄，释放时归还信号量
/// </summary>
internal sealed class MemoryLockHandle(string key, MemoryLocalLock owner) : ILockHandle
{
    public ValueTask DisposeAsync()
    {
        owner.Release(key);
        return ValueTask.CompletedTask;
    }
}

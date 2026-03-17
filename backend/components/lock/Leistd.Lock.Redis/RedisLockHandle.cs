using Leistd.Lock.Core;

namespace Leistd.Lock.Redis;

/// <summary>
/// Redis 锁句柄，持有 key + token，释放时通过 LockRelease 原子校验后删除
/// </summary>
internal sealed class RedisLockHandle(string key, string token, RedisDistributedLock owner) : ILockHandle
{
    public ValueTask DisposeAsync() => new(owner.ReleaseAsync(key, token));
}

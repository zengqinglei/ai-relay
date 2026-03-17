namespace Leistd.Lock.Core;

/// <summary>
/// 分布式锁服务接口（对应 Java IDistributedLock）
/// 标记接口，继承自 ILock，用于明确表达"需要分布式锁"的依赖意图
/// </summary>
public interface IDistributedLock : ILock
{
}

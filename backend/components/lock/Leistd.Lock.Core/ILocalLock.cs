namespace Leistd.Lock.Core;

/// <summary>
/// 本地锁服务接口（对应 Java ILocalLock）
/// 标记接口，继承自 ILock，用于明确表达"需要本地锁"的依赖意图
/// </summary>
public interface ILocalLock : ILock
{
}

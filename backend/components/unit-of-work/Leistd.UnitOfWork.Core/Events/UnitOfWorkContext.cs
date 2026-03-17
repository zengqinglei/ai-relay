namespace Leistd.UnitOfWork.Core.Events;

/// <summary>
/// 工作单元上下文 - 使用 AsyncLocal 存储当前事务阶段
/// </summary>
public static class UnitOfWorkContext
{
    private static readonly AsyncLocal<UnitOfWorkPhase?> _currentPhase = new();

    /// <summary>
    /// 当前工作单元阶段（用于事件处理器过滤）
    /// </summary>
    public static UnitOfWorkPhase? CurrentPhase
    {
        get => _currentPhase.Value;
        internal set => _currentPhase.Value = value;
    }
}

namespace Leistd.UnitOfWork.Core.Events;

/// <summary>
/// 工作单元事件阶段
/// </summary>
public enum UnitOfWorkPhase
{
    /// <summary>
    /// 提交前（SaveChanges 之后、Commit 之前）
    /// 此阶段的处理器如果抛出异常，会导致事务回滚
    /// </summary>
    BeforeCommit,

    /// <summary>
    /// 提交后（Commit 成功之后）- 默认阶段
    /// 此阶段的处理器不会影响事务，适合发送通知、更新缓存等操作
    /// </summary>
    AfterCommit,

    /// <summary>
    /// 回滚后（事务失败或回滚时）
    /// 适合补偿操作、错误日志等
    /// </summary>
    AfterRollback,

    /// <summary>
    /// 完成后（无论成功或失败，UnitOfWork Dispose 时触发）
    /// 适合资源清理、审计日志等
    /// </summary>
    AfterCompletion
}

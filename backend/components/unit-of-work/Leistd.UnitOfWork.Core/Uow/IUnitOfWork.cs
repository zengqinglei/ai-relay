using Leistd.EventBus.Core.Event;
using Leistd.UnitOfWork.Core.Database;
using Leistd.UnitOfWork.Core.Options;

namespace Leistd.UnitOfWork.Core.Uow;

/// <summary>
/// 工作单元接口
/// </summary>
public interface IUnitOfWork : IDatabaseApiContainer, ITransactionApiContainer, IDisposable
{
    /// <summary>
    /// 工作单元唯一标识
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// 工作单元配置选项
    /// </summary>
    IUnitOfWorkOptions Options { get; }

    /// <summary>
    /// 外层工作单元（嵌套场景）
    /// </summary>
    IUnitOfWork? Outer { get; }

    /// <summary>
    /// 是否已释放
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// 设置外层工作单元
    /// </summary>
    void SetOuter(IUnitOfWork? outer);

    /// <summary>
    /// 初始化工作单元
    /// </summary>
    void Initialize(UnitOfWorkOptions options);

    /// <summary>
    /// 完成工作单元（提交事务）
    /// </summary>
    Task CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚工作单元
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加待发布事件（由基础设施层调用，事件将在不同阶段发布）
    /// </summary>
    void AddPendingEvents(IEnumerable<ILocalEvent> events);
}


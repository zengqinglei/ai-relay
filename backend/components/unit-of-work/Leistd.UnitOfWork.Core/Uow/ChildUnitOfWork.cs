using Leistd.EventBus.Core.Event;
using Leistd.UnitOfWork.Core.Events;
using Leistd.UnitOfWork.Core.Database;
using Leistd.UnitOfWork.Core.Options;

namespace Leistd.UnitOfWork.Core.Uow;

/// <summary>
/// 子工作单元（委托给父工作单元处理，用于嵌套场景）
/// </summary>
internal class ChildUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _parent;

    public Guid Id => _parent.Id;
    public IUnitOfWorkOptions Options => _parent.Options;
    public IUnitOfWork? Outer => _parent.Outer;
    public bool IsDisposed => _parent.IsDisposed;
    public bool IsCompleted => _parent.IsCompleted;

    /// <summary>
    /// 框架内部事件：UnitOfWork 失败时触发
    /// </summary>
    internal event EventHandler<UnitOfWorkFailedEventArgs>? Failed;

    /// <summary>
    /// 框架内部事件：UnitOfWork 释放时触发
    /// </summary>
    internal event EventHandler<UnitOfWorkEventArgs>? Disposed;

    public ChildUnitOfWork(IUnitOfWork parent)
    {
        _parent = parent;

        // 订阅父工作单元的事件并转发（与 ABP 框架保持一致）
        if (_parent is UnitOfWork concreteParent)
        {
            concreteParent.Failed += (sender, args) => Failed?.Invoke(sender, args);
            concreteParent.Disposed += (sender, args) => Disposed?.Invoke(sender, args);
        }
    }

    public void SetOuter(IUnitOfWork? outer) => _parent.SetOuter(outer);

    public void Initialize(UnitOfWorkOptions options) => _parent.Initialize(options);

    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => _parent.RollbackAsync(cancellationToken);

    public void AddPendingEvents(IEnumerable<ILocalEvent> events) => _parent.AddPendingEvents(events);

    public IDatabaseApi GetOrAddDatabaseApi(Func<IDatabaseApi> factory) => _parent.GetOrAddDatabaseApi(factory);

    public ITransactionApi? FindTransactionApi() => _parent.FindTransactionApi();

    public void AddTransactionApi(ITransactionApi api) => _parent.AddTransactionApi(api);

    public void Dispose() { }

    public override string ToString() => $"[ChildUnitOfWork {Id}]";
}

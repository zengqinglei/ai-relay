using Leistd.EventBus.Core.Event;
using Leistd.EventBus.Core.EventBus;
using Leistd.UnitOfWork.Core.Events;
using Leistd.UnitOfWork.Core.Database;
using Leistd.UnitOfWork.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leistd.UnitOfWork.Core.Uow;

/// <summary>
/// 工作单元实现
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ILocalEventBus? _localEventBus;
    private readonly ILogger<UnitOfWork>? _logger;

    /// <inheritdoc/>
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public IUnitOfWorkOptions Options { get; private set; }

    /// <inheritdoc/>
    public IUnitOfWork? Outer { get; private set; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// 待发布的事件列表（临时收集，用于 BeforeCommit 循环处理）
    /// </summary>
    private readonly List<ILocalEvent> _pendingEvents = new();

    /// <summary>
    /// 保存到后续阶段发布的事件列表（AfterCommit, AfterRollback, AfterCompletion）
    /// </summary>
    private readonly List<ILocalEvent> _eventsForLaterPhases = new();

    /// <summary>
    /// 框架内部事件：UnitOfWork 失败时触发（仅供框架内部使用，业务层请使用 UowPhase.AfterRollback）
    /// </summary>
    internal event EventHandler<UnitOfWorkFailedEventArgs>? Failed;

    /// <summary>
    /// 框架内部事件：UnitOfWork 释放时触发（仅供框架内部使用，业务层请使用 UowPhase.AfterCompletion）
    /// </summary>
    internal event EventHandler<UnitOfWorkEventArgs>? Disposed;

    /// <inheritdoc/>
    public IServiceProvider ServiceProvider { get; }

    private IDatabaseApi? _databaseApi;
    private ITransactionApi? _transactionApi;

    private System.Exception? _exception;
    private bool _isCompleting;
    private bool _isRolledback;

    /// <inheritdoc/>
    public UnitOfWork(IServiceProvider serviceProvider, UnitOfWorkOptions options)
    {
        ServiceProvider = serviceProvider;
        Options = options.Clone();
        _localEventBus = serviceProvider.GetService<ILocalEventBus>();
        _logger = serviceProvider.GetService<ILogger<UnitOfWork>>();

        _logger?.LogDebug("创建工作单元 {UowId}", Id);
    }

    /// <inheritdoc/>
    public virtual void Initialize(UnitOfWorkOptions options)
    {
        if (options != null)
        {
            Options = options;
        }
    }

    /// <inheritdoc/>
    public virtual void SetOuter(IUnitOfWork? outer)
    {
        Outer = outer;
    }

    /// <inheritdoc/>
    public virtual async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledback)
        {
            return;
        }

        PreventMultipleComplete();

        try
        {
            _isCompleting = true;

            // ABP 风格的循环处理：SaveChanges → 发布 BeforeCommit → 重复直到无新事件
            var databaseApi = (_databaseApi as ISupportsSavingChanges);
            if (databaseApi != null)
            {
                while (true)
                {
                    await databaseApi.SaveChangesAsync(cancellationToken);

                    if (!_pendingEvents.Any())
                    {
                        break;
                    }

                    var events = _pendingEvents.ToList();
                    _pendingEvents.Clear();

                    // 发布 BeforeCommit 阶段事件
                    if (_localEventBus != null)
                    {
                        _logger?.LogDebug("工作单元 {UowId} 发布 BeforeCommit 阶段事件，共 {Count} 个", Id, events.Count);

                        UnitOfWorkContext.CurrentPhase = UnitOfWorkPhase.BeforeCommit;
                        try
                        {
                            foreach (var @event in events)
                            {
                                await _localEventBus.PublishAsync(@event, cancellationToken);
                            }
                        }
                        finally
                        {
                            UnitOfWorkContext.CurrentPhase = null;
                        }
                    }

                    _eventsForLaterPhases.AddRange(events);
                }
            }

            await CommitTransactionsAsync();
            IsCompleted = true;

            _logger?.LogDebug("工作单元 {UowId} 提交成功", Id);

            await OnCompletedAsync();
        }
        catch (System.Exception ex)
        {
            _exception = ex;
            _logger?.LogError(ex, "工作单元 {UowId} 提交失败", Id);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isRolledback)
        {
            return;
        }

        _isRolledback = true;
        _logger?.LogWarning("工作单元 {UowId} 执行回滚", Id);

        await RollbackAllAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public IDatabaseApi GetOrAddDatabaseApi(Func<IDatabaseApi> factory)
    {
        if (_databaseApi == null)
        {
            _databaseApi = factory();
        }
        return _databaseApi;
    }

    /// <inheritdoc/>
    public ITransactionApi? FindTransactionApi()
    {
        return _transactionApi;
    }

    /// <inheritdoc/>
    public void AddTransactionApi(ITransactionApi api)
    {
        _transactionApi = api;
    }

    /// <inheritdoc/>
    public void AddPendingEvents(IEnumerable<ILocalEvent> events)
    {
        _pendingEvents.AddRange(events);
    }

    /// <summary>
    /// 提交成功后处理（发布 AfterCommit 事件）
    /// </summary>
    protected virtual async Task OnCompletedAsync()
    {
        if (_localEventBus != null && _eventsForLaterPhases.Any())
        {
            _logger?.LogDebug("工作单元 {UowId} 发布 AfterCommit 阶段事件，共 {Count} 个", Id, _eventsForLaterPhases.Count);

            UnitOfWorkContext.CurrentPhase = UnitOfWorkPhase.AfterCommit;
            try
            {
                foreach (var @event in _eventsForLaterPhases)
                {
                    await _localEventBus.PublishAsync(@event, CancellationToken.None);
                }
            }
            finally
            {
                UnitOfWorkContext.CurrentPhase = null;
            }
        }
    }

    /// <summary>
    /// 失败后处理（发布 AfterRollback 事件）
    /// </summary>
    protected virtual async Task OnFailedAsync()
    {
        if (_localEventBus != null && _eventsForLaterPhases.Any())
        {
            _logger?.LogDebug("工作单元 {UowId} 发布 AfterRollback 阶段事件，共 {Count} 个", Id, _eventsForLaterPhases.Count);

            UnitOfWorkContext.CurrentPhase = UnitOfWorkPhase.AfterRollback;
            try
            {
                foreach (var @event in _eventsForLaterPhases)
                {
                    await _localEventBus.PublishAsync(@event, CancellationToken.None);
                }
            }
            catch
            {
                // 忽略 AfterRollback 处理器中的异常
            }
            finally
            {
                UnitOfWorkContext.CurrentPhase = null;
            }
        }

        Failed?.Invoke(this, new UnitOfWorkFailedEventArgs(this, _exception, _isRolledback));
    }

    /// <summary>
    /// 触发 Disposed 事件
    /// </summary>
    protected virtual void OnDisposed()
    {
        Disposed?.Invoke(this, new UnitOfWorkEventArgs(this));
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        DisposeDatabases();
        DisposeTransactions();

        if (!IsCompleted || _exception != null)
        {
            _ = OnFailedAsync();
        }

        // 发布 AfterCompletion 事件
        if (_localEventBus != null && _eventsForLaterPhases.Any())
        {
            _logger?.LogDebug("工作单元 {UowId} 发布 AfterCompletion 阶段事件，共 {Count} 个", Id, _eventsForLaterPhases.Count);

            UnitOfWorkContext.CurrentPhase = UnitOfWorkPhase.AfterCompletion;
            try
            {
                foreach (var @event in _eventsForLaterPhases)
                {
                    _ = _localEventBus.PublishAsync(@event, CancellationToken.None);
                }
            }
            finally
            {
                UnitOfWorkContext.CurrentPhase = null;
            }
        }

        _logger?.LogDebug("工作单元 {UowId} 已释放", Id);

        OnDisposed();
    }

    private void DisposeDatabases()
    {
        try
        {
            _databaseApi?.Dispose();
        }
        catch
        {
        }
    }

    private void DisposeTransactions()
    {
        try
        {
            _transactionApi?.Dispose();
        }
        catch
        {
        }
    }

    private void PreventMultipleComplete()
    {
        if (IsCompleted || _isCompleting)
        {
            throw new InvalidOperationException("工作单元已完成，不能重复调用 CompleteAsync");
        }
    }

    /// <summary>
    /// 回滚所有数据库和事务操作
    /// </summary>
    protected virtual async Task RollbackAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_databaseApi is ISupportsRollback databaseApi)
            {
                await databaseApi.RollbackAsync(cancellationToken);
            }
            if (_transactionApi is ISupportsRollback transactionApi)
            {
                await transactionApi.RollbackAsync(cancellationToken);
            }
        }
        catch
        {
        }
    }

    /// <inheritdoc/>
    protected virtual async Task CommitTransactionsAsync()
    {
        if (_transactionApi != null)
        {
            await _transactionApi.CommitAsync();
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[UnitOfWork {Id}]";
    }
}

using Leistd.Ddd.Domain.Entities;
using Leistd.EventBus.Core.Event;
using Leistd.EventBus.Core.EventBus;
using Leistd.UnitOfWork.Core.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Leistd.Ddd.Infrastructure.EventBus;

/// <summary>
/// 本地事件收集和发布拦截器
/// </summary>
/// <remarks>
/// 在 SaveChanges 完成后收集实体的本地事件，并通过 UnitOfWork 或 EventBus 发布。
/// 这是 EF Core 官方推荐的方式，确保事件发布与数据保存在同一事务上下文中。
/// </remarks>
public class LocalEventSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ILocalEventBus? _localEventBus;
    private readonly IUnitOfWorkManager? _unitOfWorkManager;
    private readonly ILogger<LocalEventSaveChangesInterceptor> _logger;

    public LocalEventSaveChangesInterceptor(
        ILocalEventBus? localEventBus,
        IUnitOfWorkManager? unitOfWorkManager,
        ILogger<LocalEventSaveChangesInterceptor> logger)
    {
        _localEventBus = localEventBus;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PublishLocalEvents(eventData.Context);
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await PublishLocalEventsAsync(eventData.Context, cancellationToken);
        return result;
    }

    /// <summary>
    /// 同步发布本地事件
    /// </summary>
    private void PublishLocalEvents(DbContext? context)
    {
        if (context == null)
            return;

        var localEvents = CollectLocalEvents(context);
        ClearLocalEvents(context);

        if (!localEvents.Any())
            return;

        var currentUow = _unitOfWorkManager?.Current;

        if (currentUow != null)
        {
            currentUow.AddPendingEvents(localEvents);
            _logger.LogDebug("收集到 {Count} 个事件，已添加到 UnitOfWork 待发布队列", localEvents.Count);
        }
        else if (_localEventBus != null)
        {
            // 警告：这是危险路径！
            _logger.LogWarning("检测到在同步 SaveChanges 中发布 {Count} 个本地事件。这可能会导致线程饥饿(Sync-over-Async)。请尽可能使用 SaveChangesAsync。", localEvents.Count);

            foreach (var @event in localEvents)
            {
                // 注意：同步发布可能会阻塞，建议使用异步版本
                _localEventBus.PublishAsync(@event).GetAwaiter().GetResult();
            }
        }
    }

    /// <summary>
    /// 异步发布本地事件
    /// </summary>
    private async Task PublishLocalEventsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null)
            return;

        var localEvents = CollectLocalEvents(context);
        ClearLocalEvents(context);

        if (!localEvents.Any())
            return;

        var currentUow = _unitOfWorkManager?.Current;

        if (currentUow != null)
        {
            currentUow.AddPendingEvents(localEvents);
            _logger.LogDebug("收集到 {Count} 个事件，已添加到 UnitOfWork 待发布队列", localEvents.Count);
        }
        else if (_localEventBus != null)
        {
            _logger.LogDebug("无 UnitOfWork，立即发布 {Count} 个事件（默认 AfterCommit 阶段）", localEvents.Count);
            foreach (var @event in localEvents)
            {
                await _localEventBus.PublishAsync(@event, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 收集实体中的本地事件
    /// </summary>
    private static List<ILocalEvent> CollectLocalEvents(DbContext context)
    {
        return context.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .SelectMany(e => e.Entity.GetLocalEvents())
            .ToList();
    }

    /// <summary>
    /// 清空实体中的本地事件
    /// </summary>
    private static void ClearLocalEvents(DbContext context)
    {
        context.ChangeTracker
            .Entries<Entity>()
            .ToList()
            .ForEach(e => e.Entity.ClearLocalEvents());
    }
}

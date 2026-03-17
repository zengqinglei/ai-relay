using Leistd.EventBus.Core.Event;
using Leistd.EventBus.Core.EventHandler;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.EventBus.Local.Wrapper;

/// <summary>
/// 事件处理器包装器（用于非泛型调用）
/// </summary>
internal abstract class EventHandlerWrapper
{
    public abstract Task HandleAsync(
        IEvent @event,
        IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken);
}

internal class EventHandlerWrapperImpl<TEvent> : EventHandlerWrapper
    where TEvent : IEvent
{
    public override async Task HandleAsync(
        IEvent @event,
        IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken)
    {
        // 创建独立的 Scope 来解析 Scoped 的 EventHandler
        using var scope = serviceScopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();
        foreach (var handler in handlers)
        {
            await handler.HandleAsync((TEvent)@event, cancellationToken);
        }
    }
}

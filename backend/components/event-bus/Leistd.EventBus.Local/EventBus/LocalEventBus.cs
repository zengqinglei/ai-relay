using Leistd.EventBus.Core.Event;
using Leistd.EventBus.Core.EventBus;
using Leistd.EventBus.Core.EventHandler;
using Leistd.EventBus.Local.Wrapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Leistd.EventBus.Local.EventBus;

/// <summary>
/// 本地事件总线实现（Singleton 生命周期）
/// 支持 Web、Console、BackgroundService 等多种应用场景
/// </summary>
public class LocalEventBus(IServiceScopeFactory serviceScopeFactory, ILogger<LocalEventBus> logger) : ILocalEventBus
{
    private static readonly ConcurrentDictionary<Type, EventHandlerWrapper> _wrapperCache = new();

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        // 创建独立的 Scope 来解析 Scoped 的 EventHandler
        using var scope = serviceScopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>().ToList();

        if (handlers.Count == 0)
        {
            // 调试级别日志，避免生产环境刷屏
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("未找到事件处理器: {EventType} (ID: {EventId})",
                    typeof(TEvent).Name, @event.EventId);
            }
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (System.Exception ex)
            {
                // 记录关键错误信息并抛出，确保事务一致性
                logger.LogError(ex, "事件处理失败: {EventType} (ID: {EventId}, Handler: {Handler})",
                    typeof(TEvent).Name, @event.EventId, handler.GetType().Name);
                throw;
            }
        }
    }

    public Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        var eventType = @event.GetType();

        var wrapper = _wrapperCache.GetOrAdd(eventType, t =>
        {
            var wrapperType = typeof(EventHandlerWrapperImpl<>).MakeGenericType(t);
            return (EventHandlerWrapper)Activator.CreateInstance(wrapperType)!;
        });

        // 委托给包装器执行，恢复泛型上下文
        return wrapper.HandleAsync(@event, serviceScopeFactory, cancellationToken);
    }
}

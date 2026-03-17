using Leistd.EventBus.Core.Event;

namespace Leistd.EventBus.Core.EventHandler;

/// <summary>
/// 事件处理器接口
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    /// <summary>
    /// 处理事件
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

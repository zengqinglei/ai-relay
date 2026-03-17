using Leistd.EventBus.Core.Event;

namespace Leistd.EventBus.Core.EventBus;

/// <summary>
/// 事件总线接口
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// 发布事件（非泛型）
    /// </summary>
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}

namespace Leistd.EventBus.Core.Event;

/// <summary>
/// 事件接口
/// </summary>
public interface IEvent
{
    /// <summary>
    /// 事件ID（用于幂等性和追踪）
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// 事件发生时间
    /// </summary>
    DateTime OccurredOn { get; }
}

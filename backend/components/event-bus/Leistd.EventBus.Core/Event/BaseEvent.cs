namespace Leistd.EventBus.Core.Event;

/// <summary>
/// 事件抽象基类
/// </summary>
[Serializable]
public abstract class BaseEvent : IEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    protected BaseEvent()
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}

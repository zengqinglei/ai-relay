namespace Leistd.EventBus.Core.Event;

/// <summary>
/// 本地事件抽象基类
/// </summary>
[Serializable]
public abstract class LocalEvent : BaseEvent, ILocalEvent
{
}

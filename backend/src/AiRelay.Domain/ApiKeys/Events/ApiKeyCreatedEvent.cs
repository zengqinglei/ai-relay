using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.ApiKeys.Events;

/// <summary>
/// API Key 创建事件
/// </summary>
public class ApiKeyCreatedEvent(Guid apiKeyId, string name) : LocalEvent
{
    public Guid ApiKeyId { get; } = apiKeyId;
    public string Name { get; } = name;
}


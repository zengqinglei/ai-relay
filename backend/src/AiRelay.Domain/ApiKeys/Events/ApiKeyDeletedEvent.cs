using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.ApiKeys.Events;

/// <summary>
/// API Key 删除事件
/// </summary>
public class ApiKeyDeletedEvent(Guid apiKeyId, string name, string keyHash) : LocalEvent
{
    public Guid ApiKeyId { get; } = apiKeyId;
    public string Name { get; } = name;
    public string KeyHash { get; } = keyHash;
}


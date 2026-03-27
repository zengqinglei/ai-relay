using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.ProviderAccounts.Events;

/// <summary>
/// 账号熔断领域事件
/// </summary>
public class AccountCircuitBrokenEvent(Guid accountId, TimeSpan lockDuration, string? description) : LocalEvent
{
    public Guid AccountId { get; } = accountId;
    public TimeSpan LockDuration { get; } = lockDuration;
    public string? Description { get; } = description;
}


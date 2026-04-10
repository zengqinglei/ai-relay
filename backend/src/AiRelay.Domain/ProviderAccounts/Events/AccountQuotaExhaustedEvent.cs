using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.ProviderAccounts.Events;

/// <summary>
/// 账号配额耗尽领域事件
/// </summary>
public class AccountQuotaExhaustedEvent(Guid accountId, Provider provider, AuthMethod authMethod) : LocalEvent
{
    public Guid AccountId { get; } = accountId;
    public Provider Provider { get; } = provider;
    public AuthMethod AuthMethod { get; } = authMethod;
}


using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.ProviderAccounts.Events;

/// <summary>
/// 账号恢复领域事件
/// </summary>
public class AccountRecoveredEvent(Guid accountId) : LocalEvent
{
    public Guid AccountId { get; } = accountId;
}


using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.ProviderAccounts.Events;

public class AccountModelCacheRefreshRequestedEvent(Guid accountId) : LocalEvent
{
    public Guid AccountId { get; } = accountId;
}

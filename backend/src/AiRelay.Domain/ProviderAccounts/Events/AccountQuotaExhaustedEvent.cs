using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.ProviderAccounts.Events;

/// <summary>
/// 账号配额耗尽领域事件
/// </summary>
public class AccountQuotaExhaustedEvent(Guid accountId, ProviderPlatform platform) : LocalEvent
{
    public Guid AccountId { get; } = accountId;
    public ProviderPlatform Platform { get; } = platform;
}


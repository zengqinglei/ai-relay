using Leistd.EventBus.Core.Event;

namespace AiRelay.Domain.ProviderAccounts.Events;

/// <summary>
/// 账号被禁用领域事件
/// </summary>
public class AccountDisabledEvent(Guid accountId, int statusCode, string reason) : LocalEvent
{
    public Guid AccountId { get; } = accountId;
    public int StatusCode { get; } = statusCode;
    public string Reason { get; } = reason;
}


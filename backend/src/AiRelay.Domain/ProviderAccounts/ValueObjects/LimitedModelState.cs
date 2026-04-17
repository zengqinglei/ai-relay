namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 模型级限流状态
/// </summary>
public class LimitedModelState
{
    public string ModelKey { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public DateTime? LockedUntil { get; init; }

    public string? StatusDescription { get; init; }

    public bool IsExpired(DateTime nowUtc)
    {
        return LockedUntil.HasValue && LockedUntil.Value <= nowUtc;
    }
}

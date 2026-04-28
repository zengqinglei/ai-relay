namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

public sealed record RouteAccountSchedulingStateSnapshot(
    IReadOnlyDictionary<Guid, int> ConcurrencyCounts,
    IReadOnlySet<Guid> RateLimitedAccountIds);

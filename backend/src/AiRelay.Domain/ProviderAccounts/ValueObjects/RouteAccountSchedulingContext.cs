namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

public sealed record RouteAccountSchedulingContext(
    string SessionHash,
    string? RequestedModel,
    IReadOnlyCollection<Guid> ExcludedAccountIds);

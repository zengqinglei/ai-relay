using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderGroups.Entities;

namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

public sealed record RouteAccountSchedulingResult(
    AccountToken AccountToken,
    ProviderGroup ProviderGroup,
    bool IsStickyBound,
    int AvailableCount);

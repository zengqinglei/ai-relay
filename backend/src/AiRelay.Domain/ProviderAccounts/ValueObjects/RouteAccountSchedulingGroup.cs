using AiRelay.Domain.ProviderGroups.Entities;

namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

public sealed record RouteAccountSchedulingGroup(
    ProviderGroup ProviderGroup,
    IReadOnlyList<ProviderGroupAccountRelation> CandidateRelations,
    int Priority);

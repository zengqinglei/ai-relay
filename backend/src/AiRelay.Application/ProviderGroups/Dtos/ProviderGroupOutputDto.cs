using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.ValueObjects;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 提供商分组输出 DTO
/// </summary>
public record ProviderGroupOutputDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public ProviderPlatform Platform { get; init; }
    public GroupSchedulingStrategy SchedulingStrategy { get; init; }
    public bool EnableStickySession { get; init; }
    public int StickySessionExpirationHours { get; init; }
    public decimal RateMultiplier { get; init; }
    public bool AllowOfficialClientMimic { get; init; }
    public DateTime CreationTime { get; init; }
    public DateTime? LastModificationTime { get; init; }
    public List<GroupAccountRelationOutputDto> Accounts { get; set; } = new();
}

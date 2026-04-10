using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 分组账户关联输出 DTO
/// </summary>
public class GroupAccountRelationOutputDto
{
    public Guid Id { get; set; }
    public Guid ProviderGroupId { get; set; }
    public Guid AccountTokenId { get; set; }
    public string AccountTokenName { get; set; } = string.Empty;
    public Provider Provider { get; set; }
    public AuthMethod AuthMethod { get; set; }
    public List<RouteProfile> SupportedRouteProfiles { get; set; } = new();
    public int Priority { get; set; }
    public int Weight { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationTime { get; set; }

    // Validity info from AccountToken
    public DateTime? ExpiresAt { get; set; }

    public int? MaxConcurrency { get; set; }
    public int CurrentConcurrency { get; set; }
}


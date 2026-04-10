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

    public GroupSchedulingStrategy SchedulingStrategy { get; init; }
    public bool EnableStickySession { get; init; }
    public int StickySessionExpirationHours { get; init; }
    public decimal RateMultiplier { get; init; }
    public DateTime CreationTime { get; init; }
    public DateTime? LastModificationTime { get; init; }
    public List<GroupAccountRelationOutputDto> Accounts { get; set; } = new();

    /// <summary>
    /// 分组内账号所能响应的路由协议（从账号的 Provider+AuthMethod 反查 RouteProfileRegistry），用于 UI 展示
    /// </summary>
    public List<RouteProfile> SupportedRouteProfiles { get; set; } = new();
}

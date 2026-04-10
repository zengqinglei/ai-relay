using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// ApiKey 分组绑定输出 DTO
/// </summary>
public class ApiKeyBindingOutputDto
{
    public Guid Id { get; set; }
    public Guid ApiKeyId { get; set; }
    public int Priority { get; set; }
    public Guid ProviderGroupId { get; set; }
    public string ProviderGroupName { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 该分组能响应的路由协议（从绑定分组的账号反查 RouteProfileRegistry），用于 UI 展示
    /// </summary>
    public List<RouteProfile> SupportedRouteProfiles { get; set; } = new();
}

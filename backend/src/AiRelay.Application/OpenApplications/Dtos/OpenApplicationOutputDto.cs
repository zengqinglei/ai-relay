using System.Text.Json;

namespace AiRelay.Application.OpenApplications.Dtos;

/// <summary>
/// 开放应用输出 DTO
/// </summary>
public record OpenApplicationOutputDto
{
    /// <summary>
    /// ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Client ID
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 应用类型
    /// </summary>
    public required string ApplicationType { get; init; }

    /// <summary>
    /// 客户端类型
    /// </summary>
    public required string ClientType { get; init; }

    /// <summary>
    /// 同意类型
    /// </summary>
    public required string ConsentType { get; init; }

    /// <summary>
    /// Redirect URIs
    /// </summary>
    public List<string> RedirectUris { get; init; } = [];

    /// <summary>
    /// Post Logout Redirect URIs
    /// </summary>
    public List<string> PostLogoutRedirectUris { get; init; } = [];

    /// <summary>
    /// 授权能力
    /// </summary>
    public List<string> Permissions { get; init; } = [];

    /// <summary>
    /// 要求
    /// </summary>
    public List<string> Requirements { get; init; } = [];

    /// <summary>
    /// 设置
    /// </summary>
    public Dictionary<string, string> Settings { get; init; } = [];

    /// <summary>
    /// 扩展属性
    /// </summary>
    public Dictionary<string, JsonElement> Properties { get; init; } = [];

    /// <summary>
    /// 是否已配置 Client Secret
    /// </summary>
    public required bool HasClientSecret { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public required DateTimeOffset CreationTime { get; init; }
}

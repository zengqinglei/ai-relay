using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.OpenApplications.Dtos;

/// <summary>
/// 更新开放应用输入 DTO
/// </summary>
public record UpdateOpenApplicationInputDto
{
    /// <summary>
    /// 显示名称
    /// </summary>
    [Display(Name = "显示名称")]
    [MaxLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// 应用类型
    /// </summary>
    [Display(Name = "应用类型")]
    [Required(ErrorMessage = "{0}不能为空")]
    public required string ApplicationType { get; init; }

    /// <summary>
    /// 客户端类型
    /// </summary>
    [Display(Name = "客户端类型")]
    [Required(ErrorMessage = "{0}不能为空")]
    public required string ClientType { get; init; }

    /// <summary>
    /// 同意类型
    /// </summary>
    [Display(Name = "同意类型")]
    [Required(ErrorMessage = "{0}不能为空")]
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
}

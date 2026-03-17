using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// 更新 API Key 输入 DTO
/// </summary>
public record UpdateApiKeyInputDto
{
    /// <summary>
    /// API Key 名称
    /// </summary>
    [Display(Name = "API Key 名称")]
    [MaxLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Name { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    [Display(Name = "API Key 描述")]
    [MaxLength(1024, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Description { get; init; }

    /// <summary>
    /// 过期时间（null 表示永不过期）
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// 绑定分组列表（全量更新）
    /// </summary>
    [Display(Name = "绑定分组")]
    public List<ApiKeyBindGroupInputDto> Bindings { get; init; } = new();
}

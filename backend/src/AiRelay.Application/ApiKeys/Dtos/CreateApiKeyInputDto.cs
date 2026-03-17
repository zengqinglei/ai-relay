using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// 创建 API Key 输入 DTO
/// </summary>
public record CreateApiKeyInputDto
{
    /// <summary>
    /// 名称
    /// </summary>
    [Display(Name = "API Key 名称")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    public required string Name { get; init; }

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
    /// 自定义密钥值（为空则自动生成，6-48位，必须包含数字和字母，可选 - 和 _）
    /// </summary>
    [Display(Name = "密钥值")]
    [RegularExpression(@"^(?=.*[0-9])(?=.*[a-zA-Z])[a-zA-Z0-9_-]{6,48}$",
        ErrorMessage = "{0}格式不正确。必须是6-48位，包含数字和字母的组合，可包含下划线(_)或连字符(-)")]
    public string? CustomSecret { get; init; }

    /// <summary>
    /// 绑定分组列表
    /// </summary>
    [Display(Name = "绑定分组")]
    public List<ApiKeyBindGroupInputDto> Bindings { get; init; } = new();
}

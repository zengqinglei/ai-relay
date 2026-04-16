using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 创建提供商分组输入 DTO
/// </summary>
public record CreateProviderGroupInputDto
{
    /// <summary>
    /// 分组名称
    /// </summary>
    [Display(Name = "分组名称")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    public required string Name { get; init; }

    /// <summary>
    /// 分组描述
    /// </summary>
    [Display(Name = "分组描述")]
    [MaxLength(1000, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Description { get; init; }

    /// <summary>
    /// 是否启用粘性会话
    /// </summary>
    [Display(Name = "启用粘性会话")]
    public bool EnableStickySession { get; init; } = false;

    /// <summary>
    /// 粘性会话过期小时数
    /// </summary>
    [Range(1, 8760, ErrorMessage = "{0}必须在{1}-{2}之间")]
    [Display(Name = "粘性会话过期小时数")]
    public int StickySessionExpirationHours { get; init; } = 1;

    /// <summary>
    /// 费率倍数
    /// </summary>
    [Range(0.01, 100, ErrorMessage = "{0}必须在{1}-{2}之间")]
    [Display(Name = "费率倍数")]
    public decimal RateMultiplier { get; init; } = 1.0m;
}

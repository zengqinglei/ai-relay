using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 添加账户到分组输入 DTO
/// </summary>
public class AddGroupAccountInputDto
{
    /// <summary>
    /// 账户TokenID
    /// </summary>
    [Required(ErrorMessage = "{0}不能为空")]
    [Display(Name = "账户TokenID")]
    public Guid AccountTokenId { get; set; }

    /// <summary>
    /// 优先级（用于 Priority 策略，值越小优先级越高）
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "{0}不能为负数")]
    [Display(Name = "优先级")]
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 权重（用于 WeightedRandom 策略）
    /// </summary>
    [Range(1, 1000, ErrorMessage = "{0}必须在{1}-{2}之间")]
    [Display(Name = "权重")]
    public int Weight { get; set; } = 1;
}

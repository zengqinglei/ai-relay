using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 更新分组账户配置输入 DTO
/// </summary>
public class UpdateGroupAccountInputDto
{
    /// <summary>
    /// 优先级
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "{0}不能为负数")]
    [Display(Name = "优先级")]
    public int Priority { get; set; }

    /// <summary>
    /// 权重
    /// </summary>
    [Range(1, 1000, ErrorMessage = "{0}必须在{1}-{2}之间")]
    [Display(Name = "权重")]
    public int Weight { get; set; }
}

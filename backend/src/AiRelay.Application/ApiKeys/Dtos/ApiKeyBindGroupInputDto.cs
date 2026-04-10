using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// ApiKey 绑定分组输入 DTO
/// </summary>
public class ApiKeyBindGroupInputDto
{
    /// <summary>
    /// 优先级 (数值越小优先级越高，如 1 为首选资源池)
    /// </summary>
    [Required(ErrorMessage = "{0}不能为空")]
    [Display(Name = "优先级")]
    public int Priority { get; set; }

    /// <summary>
    /// 分组ID
    /// </summary>
    [Required(ErrorMessage = "{0}不能为空")]
    [Display(Name = "分组ID")]
    public Guid ProviderGroupId { get; set; }
}

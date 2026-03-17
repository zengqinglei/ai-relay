using System.ComponentModel.DataAnnotations;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// ApiKey 绑定分组输入 DTO
/// </summary>
public class ApiKeyBindGroupInputDto
{
    /// <summary>
    /// 平台类型
    /// </summary>
    [Required(ErrorMessage = "{0}不能为空")]
    [Display(Name = "平台类型")]
    public ProviderPlatform Platform { get; set; }

    /// <summary>
    /// 分组ID
    /// </summary>
    [Required(ErrorMessage = "{0}不能为空")]
    [Display(Name = "分组ID")]
    public Guid ProviderGroupId { get; set; }
}

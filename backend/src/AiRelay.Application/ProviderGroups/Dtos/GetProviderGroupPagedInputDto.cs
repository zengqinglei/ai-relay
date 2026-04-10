using System.ComponentModel.DataAnnotations;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 获取提供商分组分页列表输入 DTO
/// </summary>
public record GetProviderGroupPagedInputDto : PagedRequestDto
{
    /// <summary>
    /// 分组名称（模糊搜索）
    /// </summary>
    [Display(Name = "分组名称")]
    [MaxLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Keyword { get; init; }


}

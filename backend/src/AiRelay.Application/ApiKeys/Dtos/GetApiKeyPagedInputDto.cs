using System.ComponentModel.DataAnnotations;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// 获取 API Key 分页列表输入 DTO
/// </summary>
public record GetApiKeyPagedInputDto : PagedRequestDto
{
    /// <summary>
    /// 搜索关键字（API Key 名称）
    /// </summary>
    [Display(Name = "搜索关键字")]
    [MaxLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Keyword { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [Display(Name = "是否启用")]
    public bool? IsActive { get; init; }
}

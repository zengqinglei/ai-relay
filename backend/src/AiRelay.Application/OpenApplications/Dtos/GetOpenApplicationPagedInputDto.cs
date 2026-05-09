using System.ComponentModel.DataAnnotations;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.OpenApplications.Dtos;

/// <summary>
/// 获取开放应用分页列表输入 DTO
/// </summary>
public record GetOpenApplicationPagedInputDto : PagedRequestDto
{
    /// <summary>
    /// 搜索关键字
    /// </summary>
    [Display(Name = "搜索关键字")]
    [MaxLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Keyword { get; init; }

    /// <summary>
    /// 应用类型
    /// </summary>
    public string? ApplicationType { get; init; }

    /// <summary>
    /// 客户端类型
    /// </summary>
    public string? ClientType { get; init; }
}

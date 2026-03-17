using System.ComponentModel.DataAnnotations;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 获取提供商账户分页列表输入 DTO
/// </summary>
public record GetAccountTokenPagedInputDto : PagedRequestDto
{
    /// <summary>
    /// 搜索关键词（账户名称）
    /// </summary>
    [Display(Name = "搜索关键词")]
    [MaxLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Keyword { get; init; }

    /// <summary>
    /// 平台类型筛选
    /// </summary>
    [Display(Name = "平台类型")]
    public ProviderPlatform? Platform { get; init; }

    /// <summary>
    /// 启用状态筛选 (null=全部, true=已启用, false=已禁用)
    /// </summary>
    [Display(Name = "启用状态")]
    public bool? IsActive { get; init; }
}

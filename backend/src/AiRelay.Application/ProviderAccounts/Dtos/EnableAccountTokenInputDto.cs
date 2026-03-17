using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 启用提供商账户输入 DTO
/// </summary>
public class EnableAccountTokenInputDto
{
    /// <summary>
    /// 备注（可选）
    /// </summary>
    [Display(Name = "备注")]
    [MaxLength(500, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Remark { get; init; }
}

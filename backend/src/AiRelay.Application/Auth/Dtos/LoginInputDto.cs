using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Auth.Dtos;

/// <summary>
/// 登录请求 DTO
/// </summary>
public record LoginInputDto
{
    /// <summary>
    /// 用户名或邮箱
    /// </summary>
    [Display(Name = "用户名或邮箱")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(256, MinimumLength = 3, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    public required string UsernameOrEmail { get; init; }

    /// <summary>
    /// 密码
    /// </summary>
    [Display(Name = "密码")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    public required string Password { get; init; }
}

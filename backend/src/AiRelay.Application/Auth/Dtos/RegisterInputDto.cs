using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Auth.Dtos;

/// <summary>
/// 注册请求 DTO
/// </summary>
public record RegisterInputDto
{
    [Display(Name = "用户名")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(64, MinimumLength = 3, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "{0}只能包含字母、数字和下划线")]
    public required string Username { get; init; }

    [Display(Name = "邮箱")]
    [Required(ErrorMessage = "{0}不能为空")]
    [EmailAddress(ErrorMessage = "{0}格式不正确")]
    [StringLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public required string Email { get; init; }

    [Display(Name = "密码")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "{0}必须包含至少一个大写字母、一个小写字母和一个数字")]
    public required string Password { get; init; }

    [Display(Name = "昵称")]
    [StringLength(128, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Nickname { get; init; }
}

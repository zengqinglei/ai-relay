using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Auth.Dtos;

public record UpdateCurrentUserInputDto
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

    [Display(Name = "昵称")]
    [StringLength(128, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Nickname { get; init; }

    [Display(Name = "手机号")]
    [MaxLength(20, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    [RegularExpression(@"^[0-9+\-()\s]{0,20}$", ErrorMessage = "{0}格式不正确")]
    public string? PhoneNumber { get; init; }

    [Display(Name = "头像")]
    [StringLength(1500000, ErrorMessage = "{0}内容过大，请压缩后重试")]
    public string? Avatar { get; init; }
}

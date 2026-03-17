using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Users.Dtos;

/// <summary>
/// 更新用户输入 DTO
/// </summary>
public record UpdateUserInputDto
{
    [Display(Name = "昵称")]
    [StringLength(128, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Nickname { get; init; }

    [Display(Name = "手机号")]
    [MaxLength(20, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? PhoneNumber { get; init; }

    [Display(Name = "头像URL")]
    [MaxLength(512, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    [Url(ErrorMessage = "{0}格式不正确")]
    public string? AvatarUrl { get; init; }

    public bool? IsActive { get; init; }
}

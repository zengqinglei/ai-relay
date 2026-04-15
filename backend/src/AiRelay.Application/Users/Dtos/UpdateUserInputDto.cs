using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Users.Dtos;

public record UpdateUserInputDto
{
    [Display(Name = "昵称")]
    [StringLength(128, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Nickname { get; init; }

    [Display(Name = "手机号")]
    [MaxLength(20, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? PhoneNumber { get; init; }

    [Display(Name = "头像")]
    [StringLength(1500000, ErrorMessage = "{0}内容过大，请压缩后重试")]
    public string? Avatar { get; init; }

    public bool? IsActive { get; init; }
}

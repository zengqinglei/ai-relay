using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Users.Dtos;

/// <summary>
/// 更新用户输入 DTO
/// </summary>
public record UpdateUserInputDto
{
    [Display(Name = "邮箱")]
    [Required(ErrorMessage = "{0}不能为空")]
    [EmailAddress(ErrorMessage = "{0}格式不正确")]
    [StringLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public required string Email { get; init; }

    [Display(Name = "显示名称")]
    [StringLength(128, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? DisplayName { get; init; }

    [Display(Name = "头像")]
    [StringLength(1500000, ErrorMessage = "{0}内容过大，请压缩后重试")]
    public string? Avatar { get; init; }

    public bool IsActive { get; init; }

    public bool IsEmailVerified { get; init; }

    public List<string> Roles { get; init; } = [];
}

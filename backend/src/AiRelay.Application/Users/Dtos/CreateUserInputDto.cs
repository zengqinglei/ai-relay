using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Users.Dtos;

/// <summary>
/// 创建用户输入 DTO
/// </summary>
public record CreateUserInputDto
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
    public required string Password { get; init; }

    [Display(Name = "昵称")]
    [StringLength(128, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Nickname { get; init; }

    /// <summary>
    /// 角色ID列表
    /// </summary>
    public List<Guid>? RoleIds { get; init; }
}

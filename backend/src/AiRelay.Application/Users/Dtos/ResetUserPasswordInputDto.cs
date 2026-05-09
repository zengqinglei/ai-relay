using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Users.Dtos;

/// <summary>
/// 重置用户密码输入 DTO
/// </summary>
public record ResetUserPasswordInputDto
{
    [Display(Name = "密码")]
    [Required(ErrorMessage = "{0}不能为空")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,20}$", ErrorMessage = "{0}需为 8~20 位并同时包含大小写字母、数字和特殊字符")]
    public required string Password { get; init; }
}

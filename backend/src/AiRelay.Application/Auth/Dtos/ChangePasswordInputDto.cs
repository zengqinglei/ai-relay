using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Auth.Dtos;

public record ChangePasswordInputDto
{
    [Display(Name = "当前密码")]
    [Required(ErrorMessage = "{0}不能为空")]
    public required string CurrentPassword { get; init; }

    [Display(Name = "新密码")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(20, MinimumLength = 8, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$", ErrorMessage = "{0}必须包含大小写字母、数字和特殊字符")]
    public required string NewPassword { get; init; }

    [Display(Name = "确认密码")]
    [Required(ErrorMessage = "{0}不能为空")]
    public required string ConfirmPassword { get; init; }
}

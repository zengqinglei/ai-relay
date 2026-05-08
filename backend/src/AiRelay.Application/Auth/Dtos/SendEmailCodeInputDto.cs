using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Auth.Dtos;

public record SendEmailCodeInputDto
{
    [Display(Name = "邮箱")]
    [Required(ErrorMessage = "请输入{0}")]
    [EmailAddress(ErrorMessage = "{0}格式不正确")]
    public required string Email { get; init; }

    [Display(Name = "验证码凭据")]
    [Required(ErrorMessage = "图形验证码已失效，请刷新重试")]
    public required string CaptchaToken { get; init; }

    [Display(Name = "图形验证码")]
    [Required(ErrorMessage = "请输入{0}")]
    public required string CaptchaCode { get; init; }
}

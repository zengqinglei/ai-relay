using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ProviderAccounts.Dtos;

[Serializable]
public record AccountTokenInputDto
{
    [Display(Name = "账户名称")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    public required string Name { get; init; }

    [Display(Name = "额外属性")]
    public Dictionary<string, string>? ExtraProperties { get; set; }

    public string? AuthCode { get; init; }

    public string? CodeVerifier { get; init; }

    public string? AccessToken { get; set; }

    [Display(Name = "Base URL")]
    [MaxLength(512, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    [Url(ErrorMessage = "{0}格式不正确")]
    public string? BaseUrl { get; init; }
}

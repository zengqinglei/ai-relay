using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.Auth.Dtos;

/// <summary>
/// 外部登录回调请求
/// </summary>
public record ExternalLoginCallbackInputDto
{
    /// <summary>
    /// 授权码
    /// </summary>
    [Required(ErrorMessage = "授权码不能为空")]
    [StringLength(500, ErrorMessage = "授权码长度不能超过 500 个字符")]
    public required string Code { get; init; }

    /// <summary>
    /// 状态参数
    /// </summary>
    [StringLength(100, ErrorMessage = "状态参数长度不能超过 100 个字符")]
    public string? State { get; init; }
}

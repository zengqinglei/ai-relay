namespace AiRelay.Application.Auth.Dtos;

/// <summary>
/// 外部登录 URL 响应
/// </summary>
public record ExternalLoginUrlOutputDto
{
    /// <summary>
    /// 登录 URL
    /// </summary>
    public required string LoginUrl { get; init; }

    /// <summary>
    /// 状态参数
    /// </summary>
    public required string State { get; init; }
}

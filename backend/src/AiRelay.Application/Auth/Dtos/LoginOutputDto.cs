namespace AiRelay.Application.Auth.Dtos;

/// <summary>
/// 登录响应 DTO
/// </summary>
public record LoginOutputDto
{
    /// <summary>
    /// 访问令牌
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// 刷新令牌
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public required int ExpiresIn { get; init; }
}

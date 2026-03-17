namespace AiRelay.Domain.Shared.OAuth.Authorize.ValueObjects;

/// <summary>
/// 外部用户信息值对象
/// </summary>
public record ExternalUserInfo
{
    /// <summary>
    /// 外部提供商的用户ID
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// 用户名
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// 昵称
    /// </summary>
    public string? Nickname { get; init; }

    /// <summary>
    /// 头像URL
    /// </summary>
    public string? AvatarUrl { get; init; }
}

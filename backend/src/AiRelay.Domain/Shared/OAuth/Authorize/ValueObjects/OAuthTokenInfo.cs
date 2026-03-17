namespace AiRelay.Domain.Shared.OAuth.Authorize.ValueObjects;

/// <summary>
/// OAuth 令牌信息值对象
/// </summary>
public record OAuthTokenInfo
{
    /// <summary>
    /// 访问令牌
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// 令牌类型（通常是 Bearer）
    /// </summary>
    public string? TokenType { get; init; }

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public int? ExpiresIn { get; init; }

    /// <summary>
    /// 刷新令牌
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// 作用域
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// 额外属性（存储平台特定元数据，如 chatgpt_account_id, project_id）
    /// </summary>
    public Dictionary<string, string>? ExtraProperties { get; init; }
}

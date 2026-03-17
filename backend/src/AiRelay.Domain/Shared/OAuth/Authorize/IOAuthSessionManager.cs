namespace AiRelay.Domain.Shared.OAuth.Authorize;

/// <summary>
/// OAuth 会话数据
/// </summary>
public class OAuthSession
{
    public required string CodeVerifier { get; set; }
    public required string State { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// OAuth 会话管理器接口 (用于 PKCE)
/// </summary>
public interface IOAuthSessionManager
{
    /// <summary>
    /// 创建并存储会话
    /// </summary>
    /// <returns>Session ID</returns>
    Task<string> CreateSessionAsync(OAuthSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取并移除会话 (一次性使用)
    /// </summary>
    Task<OAuthSession?> GetAndRemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

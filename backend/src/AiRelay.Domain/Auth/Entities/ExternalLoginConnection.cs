using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.Auth.Entities;

/// <summary>
/// 外部登录连接实体（GitHub、Google 等第三方身份提供商）
/// </summary>
public class ExternalLoginConnection : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// 外部身份提供商（GitHub, Google）
    /// </summary>
    public string Provider { get; private set; }

    /// <summary>
    /// 外部身份提供商的用户 ID
    /// </summary>
    public string ProviderUserId { get; private set; }

    /// <summary>
    /// 外部身份提供商的用户名
    /// </summary>
    public string? ProviderUsername { get; private set; }

    /// <summary>
    /// 外部身份提供商的邮箱
    /// </summary>
    public string? ProviderEmail { get; private set; }

    /// <summary>
    /// 外部身份提供商的头像 URL
    /// </summary>
    public string? ProviderAvatarUrl { get; private set; }

    /// <summary>
    /// Access Token（加密存储）
    /// </summary>
    public string? AccessToken { get; private set; }

    /// <summary>
    /// Refresh Token（加密存储）
    /// </summary>
    public string? RefreshToken { get; private set; }

    /// <summary>
    /// Token 过期时间
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// 最后同步时间
    /// </summary>
    public DateTime? LastSyncTime { get; private set; }

    /// <summary>
    /// 导航属性 - 用户
    /// </summary>
    public User? User { get; private set; }

    private ExternalLoginConnection()
    {
        Provider = null!;
        ProviderUserId = null!;
    }

    public ExternalLoginConnection(
        Guid userId,
        string provider,
        string providerUserId,
        string? providerUsername = null,
        string? providerEmail = null,
        string? providerAvatarUrl = null)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        ProviderUserId = providerUserId ?? throw new ArgumentNullException(nameof(providerUserId));
        ProviderUsername = providerUsername;
        ProviderEmail = providerEmail;
        ProviderAvatarUrl = providerAvatarUrl;
        LastSyncTime = DateTime.UtcNow;
    }

    public void Update(
        string? providerUsername = null,
        string? providerEmail = null,
        string? providerAvatarUrl = null)
    {
        ProviderUsername = providerUsername;
        ProviderEmail = providerEmail;
        ProviderAvatarUrl = providerAvatarUrl;
        LastSyncTime = DateTime.UtcNow;
    }

    public void UpdateTokens(
        string? accessToken,
        string? refreshToken = null,
        DateTime? expiresAt = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAt = expiresAt;
        LastSyncTime = DateTime.UtcNow;
    }

    public bool IsTokenExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    }
}

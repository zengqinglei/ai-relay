namespace AiRelay.Domain.Shared.OAuth.Google;

/// <summary>
/// Google OAuth 配置模型
/// </summary>
public class GoogleAuthConfig
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string RedirectUri { get; set; }
    public required string Scopes { get; set; }
}

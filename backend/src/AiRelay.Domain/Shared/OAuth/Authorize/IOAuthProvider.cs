using AiRelay.Domain.Shared.OAuth.Authorize.ValueObjects;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Domain.Shared.OAuth.Authorize;

/// <summary>
/// OAuth 提供商服务基础接口
/// </summary>
public interface IOAuthProvider
{
    /// <summary>
    /// 获取授权 URL (标准)
    /// </summary>
    string GetAuthorizationUrl(string redirectUri, string state);

    /// <summary>
    /// 获取授权 URL (支持 PKCE 和多平台配置)
    /// </summary>
    /// <param name="provider">提供商</param>
    /// <param name="state">状态码</param>
    /// <param name="codeChallenge">PKCE Code Challenge</param>
    /// <returns>授权 URL</returns>
    string GetAuthorizationUrl(Provider provider, string state, string codeChallenge);

    /// <summary>
    /// 使用 Authorization Code 换取 Token
    /// </summary>
    /// <param name="code">授权码</param>
    /// <param name="redirectUri">重定向 URI (可选，部分平台由配置决定)</param>
    /// <param name="codeVerifier">PKCE Code Verifier (可选)</param>
    /// <param name="provider">提供商</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>OAuth Token 信息</returns>
    Task<OAuthTokenInfo> ExchangeCodeForTokenAsync(
        string code,
        string? redirectUri = null,
        string? codeVerifier = null,
        Provider? provider = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取第三方用户信息
    /// </summary>
    /// <param name="accessToken">访问令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户信息</returns>
    Task<ExternalUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新访问令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="provider">提供商</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>OAuth Token 信息</returns>
    Task<OAuthTokenInfo> RefreshTokenAsync(string refreshToken, Provider provider, CancellationToken cancellationToken = default);
}

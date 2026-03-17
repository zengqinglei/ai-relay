using System.Net.Http.Json;
using System.Text.Json;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.Shared.OAuth.Authorize.ValueObjects;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Exception.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.OAuth.Authorize;

/// <summary>
/// GitHub OAuth 服务实现
/// </summary>
public class GitHubOAuthProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GitHubOAuthProvider> logger) : IOAuthProvider
{
    private const string AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string UserInfoEndpoint = "https://api.github.com/user";

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        var clientId = configuration["ExternalAuth:GitHub:ClientId"]
            ?? throw new NotFoundException("GitHub ClientId 未配置");

        return $"{AuthorizationEndpoint}?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}&scope=user:email";
    }

    public string GetAuthorizationUrl(ProviderPlatform platform, string state, string codeChallenge)
    {
        throw new BadRequestException("GitHub OAuth provider does not support PKCE flow.");
    }

    public async Task<OAuthTokenInfo> ExchangeCodeForTokenAsync(
        string code,
        string? redirectUri = null,
        string? codeVerifier = null,
        ProviderPlatform? platform = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(redirectUri))
            throw new BadRequestException("GitHub OAuth requires redirectUri");

        var clientId = configuration["ExternalAuth:GitHub:ClientId"]
            ?? throw new NotFoundException("GitHub ClientId 未配置");
        var clientSecret = configuration["ExternalAuth:GitHub:ClientSecret"]
            ?? throw new NotFoundException("GitHub ClientSecret 未配置");

        var httpClient = httpClientFactory.CreateClient();

        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        };

        var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(requestData), cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var queryParams = System.Web.HttpUtility.ParseQueryString(responseContent);
        var accessToken = queryParams["access_token"];

        if (string.IsNullOrEmpty(accessToken))
        {
            logger.LogError("获取 GitHub Access Token 失败: {Response}", responseContent);
            throw new BadRequestException("获取 GitHub Access Token 失败");
        }

        return new OAuthTokenInfo
        {
            AccessToken = accessToken,
            TokenType = queryParams["token_type"],
            Scope = queryParams["scope"]
        };
    }

    public async Task<ExternalUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "AiRelay-App");

        var response = await httpClient.GetAsync(UserInfoEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var userInfo = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(cancellationToken);
        if (userInfo == null)
            throw new BadRequestException("获取 GitHub 用户信息失败");

        return new ExternalUserInfo
        {
            ProviderId = userInfo["id"].GetInt64().ToString(),
            Email = userInfo.TryGetValue("email", out var email) ? email.GetString() : null,
            Username = userInfo["login"].GetString()!,
            Nickname = userInfo.TryGetValue("name", out var name) ? name.GetString() : null,
            AvatarUrl = userInfo.TryGetValue("avatar_url", out var avatar) ? avatar.GetString() : null
        };
    }

    public Task<OAuthTokenInfo> RefreshTokenAsync(string refreshToken, ProviderPlatform platform, CancellationToken cancellationToken = default)
    {
        throw new BadRequestException("GitHub OAuth does not support token refresh in this context.");
    }
}

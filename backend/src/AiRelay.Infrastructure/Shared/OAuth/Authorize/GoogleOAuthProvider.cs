using System.Net.Http.Json;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.Shared.OAuth.Authorize.ValueObjects;
using AiRelay.Domain.Shared.OAuth.Google;
using Leistd.Exception.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.OAuth.Authorize;

/// <summary>
/// Google OAuth 服务实现 (支持 Gemini 和 Antigravity 多配置)
/// </summary>
public class GoogleOAuthProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration, // 保留用于兼容旧的 github oauth 等
    IGoogleAuthConfigService authConfigService,
    ILogger<GoogleOAuthProvider> logger) : IOAuthProvider
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";

    // 旧的 GetAuthorizationUrl 实现（如果仍有其他地方调用且不传平台，则保留或标记为过时）
    // 这里我们假设主要流程已经切换到 GenerateAuthUrl，或者此方法只用于默认的 Google 登录（如后台管理登录）
    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        // 兼容现有逻辑，读取配置文件中的 Google ClientId
        // 如果没有配置，可以抛出异常或回退到默认
        var clientId = configuration["ExternalAuth:Google:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            // 如果未配置，尝试从 ConfigService 获取默认的（例如 Gemini）
            // 但通常 ExternalAuth:Google 是用于系统登录的
            throw new NotFoundException("Google ClientId 未配置");
        }

        return $"{AuthorizationEndpoint}?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope=openid%20email%20profile&state={state}";
    }

    /// <summary>
    /// 获取授权 URL (支持 PKCE 和多平台配置)
    /// </summary>
    public string GetAuthorizationUrl(Provider provider, string state, string codeChallenge)
    {
        var config = authConfigService.GetConfig(provider);

        return $"{AuthorizationEndpoint}?" +
               $"client_id={config.ClientId}&" +
               $"redirect_uri={Uri.EscapeDataString(config.RedirectUri)}&" +
               $"response_type=code&" +
               $"scope={Uri.EscapeDataString(config.Scopes)}&" +
               $"state={state}&" +
               $"code_challenge={codeChallenge}&" +
               $"code_challenge_method=S256&" +
               $"access_type=offline&" +
               $"prompt=consent&" +
               $"include_granted_scopes=true";
    }

    public async Task<OAuthTokenInfo> ExchangeCodeForTokenAsync(
        string code,
        string? redirectUri = null,
        string? codeVerifier = null,
        Provider? provider = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 系统登录模式 (Legacy): 提供了 redirectUri 但没有 provider
        if (provider == null && !string.IsNullOrEmpty(redirectUri))
        {
            var sysClientId = configuration["ExternalAuth:Google:ClientId"]
                ?? throw new NotFoundException("Google ClientId 未配置");
            var sysClientSecret = configuration["ExternalAuth:Google:ClientSecret"]
                ?? throw new NotFoundException("Google ClientSecret 未配置");

            return await ExchangeInternalAsync(sysClientId, sysClientSecret, code, redirectUri, codeVerifier, cancellationToken);
        }

        // 2. 账号绑定模式: 必须提供 Provider
        if (provider == null)
        {
            // 默认为 Gemini 以保持兼容性
            provider = Provider.Gemini;
        }

        var config = authConfigService.GetConfig(provider.Value);

        // 如果调用方没有提供 redirectUri，使用配置中的
        var effectiveRedirectUri = !string.IsNullOrEmpty(redirectUri) ? redirectUri : config.RedirectUri;

        return await ExchangeInternalAsync(config.ClientId, config.ClientSecret, code, effectiveRedirectUri, codeVerifier, cancellationToken);
    }

    public async Task<ExternalUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        var response = await httpClient.GetAsync(UserInfoEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var userInfo = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(cancellationToken);
        if (userInfo == null)
            throw new BadRequestException("获取 Google 用户信息失败");

        return new ExternalUserInfo
        {
            ProviderId = userInfo["id"].GetString() ?? throw new BadRequestException("Google User ID is missing"),
            Email = userInfo.TryGetValue("email", out var email) ? email.GetString() : null,
            Username = (userInfo.TryGetValue("email", out var uEmail) ? uEmail.GetString()?.Split('@')[0] : null)
                       ?? userInfo["id"].GetString()!,
            Nickname = userInfo.TryGetValue("name", out var name) ? name.GetString() : null,
            AvatarUrl = userInfo.TryGetValue("picture", out var picture) ? picture.GetString() : null
        };
    }

    // 内部辅助方法，复用 HTTP 请求逻辑
    private async Task<OAuthTokenInfo> ExchangeInternalAsync(
        string clientId,
        string clientSecret,
        string code,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient();

        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        };

        if (!string.IsNullOrEmpty(codeVerifier))
        {
            requestData["code_verifier"] = codeVerifier;
        }

        var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(requestData), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Google Token 交换失败: {StatusCode} {Content}", response.StatusCode, errorContent);
            throw new BadRequestException($"获取 Access Token 失败: {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

        if (tokenResponse == null || !tokenResponse.TryGetValue("access_token", out var accessTokenElement))
        {
            logger.LogError("解析 Google Access Token 失败: {Response}", responseContent);
            throw new BadRequestException("解析 Google Access Token 失败");
        }

        return new OAuthTokenInfo
        {
            AccessToken = accessTokenElement.GetString() ?? throw new BadRequestException("Access Token 为空"),
            TokenType = tokenResponse.TryGetValue("token_type", out var tokenType) ? tokenType.GetString() : null,
            ExpiresIn = tokenResponse.TryGetValue("expires_in", out var expiresIn) ? expiresIn.GetInt32() : null,
            RefreshToken = tokenResponse.TryGetValue("refresh_token", out var refreshToken) ? refreshToken.GetString() : null,
            Scope = tokenResponse.TryGetValue("scope", out var scope) ? scope.GetString() : null
        };
    }

    public async Task<OAuthTokenInfo> RefreshTokenAsync(string refreshToken, Provider provider, CancellationToken cancellationToken = default)
    {
        var config = authConfigService.GetConfig(provider);

        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };

        var httpClient = httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(requestBody);
        var response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (errorContent.Contains("invalid_grant") || errorContent.Contains("invalid_client"))
                {
                    throw new UnauthorizedException($"Google Token Refresh Failed: {errorContent}");
                }
                throw new BadRequestException($"Google Token Refresh Failed: {errorContent}");
            }

            throw new InternalServerException($"Google Token Service Error: {response.StatusCode} {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseJson);

        if (tokenResponse == null || !tokenResponse.TryGetValue("access_token", out var accessToken))
        {
            throw new InternalServerException("Google Token Refresh Response Invalid");
        }

        return new OAuthTokenInfo
        {
            AccessToken = accessToken.GetString()!,
            RefreshToken = tokenResponse.TryGetValue("refresh_token", out var rt) && rt.GetString() != null ? rt.GetString() : refreshToken,
            ExpiresIn = tokenResponse.TryGetValue("expires_in", out var exp) ? exp.GetInt32() : 3599,
            TokenType = tokenResponse.TryGetValue("token_type", out var tt) ? tt.GetString() : "Bearer"
        };
    }
}

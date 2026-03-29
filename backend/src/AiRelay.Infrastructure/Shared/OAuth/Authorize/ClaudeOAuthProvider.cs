using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.Shared.OAuth.Authorize.ValueObjects;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.OAuth.Authorize;

/// <summary>
/// Claude (Anthropic) OAuth 服务实现
/// </summary>
public class ClaudeOAuthProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<ClaudeOAuthProvider> logger) : IOAuthProvider
{
    private const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    private const string AuthorizationEndpoint = "https://claude.ai/oauth/authorize";
    private const string TokenEndpoint = "https://platform.claude.com/v1/oauth/token";
    private const string UserInfoEndpoint = "https://api.anthropic.com/v1/users/me"; // 假设，实际可能不同，Claude 官方 API 主要是 completions
    private const string RedirectUri = "https://platform.claude.com/oauth/code/callback";
    private const string Scope = "user:profile user:inference user:sessions:claude_code user:mcp_servers";

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        // 兼容旧接口，使用默认 RedirectUri
        return GetAuthorizationUrl(ProviderPlatform.CLAUDE_OAUTH, state, "");
    }

    public string GetAuthorizationUrl(ProviderPlatform platform, string state, string codeChallenge)
    {
        return $"{AuthorizationEndpoint}?" +
               $"client_id={ClientId}&" +
               $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
               $"response_type=code&" +
               $"scope={Uri.EscapeDataString(Scope)}&" +
               $"state={state}&" +
               $"code_challenge={codeChallenge}&" +
               $"code_challenge_method=S256";
    }

    public async Task<OAuthTokenInfo> ExchangeCodeForTokenAsync(
        string code,
        string? redirectUri = null,
        string? codeVerifier = null,
        ProviderPlatform? platform = null,
        CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();

        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri, // 使用常量 RedirectUri
            ["code_verifier"] = codeVerifier ?? ""
        };

        var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(requestBody), cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Claude Token 交换失败: {StatusCode} {Content}", response.StatusCode, content);
            throw new BadRequestException($"Claude Token 交换失败: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
        if (tokenResponse == null || !tokenResponse.TryGetValue("access_token", out var accessToken))
        {
            throw new BadRequestException("解析 Claude Token 响应失败");
        }

        // 解析 account.uuid（用于 metadata.user_id 注入）
        Dictionary<string, string>? extraProperties = null;
        if (tokenResponse.TryGetValue("account", out var accountEl) &&
            accountEl.ValueKind == JsonValueKind.Object &&
            accountEl.TryGetProperty("uuid", out var uuidEl))
        {
            var uuid = uuidEl.GetString();
            if (!string.IsNullOrEmpty(uuid))
                extraProperties = new Dictionary<string, string> { ["account_uuid"] = uuid };
        }

        return new OAuthTokenInfo
        {
            AccessToken = accessToken.GetString()!,
            RefreshToken = tokenResponse.TryGetValue("refresh_token", out var rt) ? rt.GetString() : null,
            ExpiresIn = tokenResponse.TryGetValue("expires_in", out var exp) ? exp.GetInt32() : 3600,
            TokenType = tokenResponse.TryGetValue("token_type", out var tt) ? tt.GetString() : "Bearer",
            ExtraProperties = extraProperties
        };
    }

    public Task<ExternalUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        // Claude 暂时没有标准的用户信息端点文档，或者我们不需要它
        // 这里返回空或模拟数据，或者抛出不支持
        return Task.FromResult(new ExternalUserInfo
        {
            ProviderId = "claude_user", // Placeholder
            Username = "claude_user"
        });
    }

    public async Task<OAuthTokenInfo> RefreshTokenAsync(string refreshToken, ProviderPlatform platform, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();

        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["redirect_uri"] = RedirectUri
        };

        var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(requestBody), cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new UnauthorizedException($"Claude Token Refresh Failed: {content}");

            throw new InternalServerException($"Claude Token Service Error: {response.StatusCode} {content}");
        }

        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
        if (tokenResponse == null || !tokenResponse.TryGetValue("access_token", out var accessToken))
        {
            throw new InternalServerException("Claude Token Refresh Response Invalid");
        }

        // 解析 account.uuid（刷新时也可能返回）
        Dictionary<string, string>? extraProperties = null;
        if (tokenResponse.TryGetValue("account", out var accountEl) &&
            accountEl.ValueKind == JsonValueKind.Object &&
            accountEl.TryGetProperty("uuid", out var uuidEl))
        {
            var uuid = uuidEl.GetString();
            if (!string.IsNullOrEmpty(uuid))
                extraProperties = new Dictionary<string, string> { ["account_uuid"] = uuid };
        }

        return new OAuthTokenInfo
        {
            AccessToken = accessToken.GetString()!,
            RefreshToken = tokenResponse.TryGetValue("refresh_token", out var rt) ? rt.GetString() : refreshToken,
            ExpiresIn = tokenResponse.TryGetValue("expires_in", out var exp) ? exp.GetInt32() : 3600,
            TokenType = tokenResponse.TryGetValue("token_type", out var tt) ? tt.GetString() : "Bearer",
            ExtraProperties = extraProperties
        };
    }
}

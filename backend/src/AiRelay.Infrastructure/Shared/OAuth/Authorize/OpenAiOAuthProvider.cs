using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.Shared.OAuth.Authorize.ValueObjects;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.OAuth.Authorize;

/// <summary>
/// OpenAI OAuth 服务实现
/// </summary>
public class OpenAiOAuthProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiOAuthProvider> logger) : IOAuthProvider
{
    private const string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    private const string AuthorizationEndpoint = "https://auth.openai.com/oauth/authorize";
    private const string TokenEndpoint = "https://auth.openai.com/oauth/token";
    private const string RedirectUri = "http://localhost:1455/auth/callback";
    private const string Scope = "openid profile email offline_access";

    /// <summary>
    /// 从 JWT token 中提取 payload（不验证签名）
    /// </summary>
    private Dictionary<string, JsonElement>? ParseJwtPayload(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
                return null;

            // Base64Url 解码 payload 部分
            var payload = parts[1];
            // 补齐 padding
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            // 替换 Base64Url 字符
            payload = payload.Replace('-', '+').Replace('_', '/');

            var payloadBytes = Convert.FromBase64String(payload);
            var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);

            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "解析 JWT payload 失败");
            return null;
        }
    }

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        return GetAuthorizationUrl(Provider.OpenAI, state, "");
    }

    public string GetAuthorizationUrl(Provider provider, string state, string codeChallenge)
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
        Provider? provider = null,
        CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();

        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = codeVerifier ?? ""
        };

        var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(requestBody), cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("OpenAI Token 交换失败: {StatusCode} {Content}", response.StatusCode, content);
            throw new BadRequestException($"OpenAI Token 交换失败: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
        if (tokenResponse == null || !tokenResponse.TryGetValue("access_token", out var accessToken))
        {
            throw new BadRequestException("解析 OpenAI Token 响应失败");
        }

        var accessTokenString = accessToken.GetString()!;

        // 解析 JWT token 提取 chatgpt_account_id
        var extraProperties = new Dictionary<string, string>();
        var jwtPayload = ParseJwtPayload(accessTokenString);
        if (jwtPayload != null)
        {
            // chatgpt_account_id 嵌套在 "https://api.openai.com/auth" 对象中
            if (jwtPayload.TryGetValue("https://api.openai.com/auth", out var authObj) &&
                authObj.ValueKind == JsonValueKind.Object)
            {
                var authDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(authObj.GetRawText());
                if (authDict != null && authDict.TryGetValue("chatgpt_account_id", out var accountId))
                {
                    var accountIdStr = accountId.GetString();
                    if (!string.IsNullOrWhiteSpace(accountIdStr))
                    {
                        extraProperties["chatgpt_account_id"] = accountIdStr;
                        logger.LogInformation("从 JWT token 中提取 chatgpt_account_id: {AccountId}", accountIdStr);
                    }
                }
            }
        }

        return new OAuthTokenInfo
        {
            AccessToken = accessTokenString,
            RefreshToken = tokenResponse.TryGetValue("refresh_token", out var rt) ? rt.GetString() : null,
            ExpiresIn = tokenResponse.TryGetValue("expires_in", out var exp) ? exp.GetInt32() : 3600,
            TokenType = tokenResponse.TryGetValue("token_type", out var tt) ? tt.GetString() : "Bearer",
            ExtraProperties = extraProperties.Count > 0 ? extraProperties : null
        };
    }

    public Task<ExternalUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        // OpenAI UserInfo Endpoint? 
        // 暂时不支持获取用户信息，仅用于 Token
        return Task.FromResult(new ExternalUserInfo
        {
            ProviderId = "openai_user",
            Username = "openai_user"
        });
    }

    public async Task<OAuthTokenInfo> RefreshTokenAsync(string refreshToken, Provider provider, CancellationToken cancellationToken = default)
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
                throw new UnauthorizedException($"OpenAI Token Refresh Failed: {content}");

            throw new InternalServerException($"OpenAI Token Service Error: {response.StatusCode} {content}");
        }

        var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
        if (tokenResponse == null || !tokenResponse.TryGetValue("access_token", out var accessToken))
        {
            throw new InternalServerException("OpenAI Token Refresh Response Invalid");
        }

        var accessTokenString = accessToken.GetString()!;

        // 解析 JWT token 提取 chatgpt_account_id
        var extraProperties = new Dictionary<string, string>();
        var jwtPayload = ParseJwtPayload(accessTokenString);
        if (jwtPayload != null)
        {
            // chatgpt_account_id 嵌套在 "https://api.openai.com/auth" 对象中
            if (jwtPayload.TryGetValue("https://api.openai.com/auth", out var authObj) &&
                authObj.ValueKind == JsonValueKind.Object)
            {
                var authDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(authObj.GetRawText());
                if (authDict != null && authDict.TryGetValue("chatgpt_account_id", out var accountId))
                {
                    var accountIdStr = accountId.GetString();
                    if (!string.IsNullOrWhiteSpace(accountIdStr))
                    {
                        extraProperties["chatgpt_account_id"] = accountIdStr;
                        logger.LogInformation("从刷新的 JWT token 中提取 chatgpt_account_id: {AccountId}", accountIdStr);
                    }
                }
            }
        }

        return new OAuthTokenInfo
        {
            AccessToken = accessTokenString,
            RefreshToken = tokenResponse.TryGetValue("refresh_token", out var rt) ? rt.GetString() : refreshToken,
            ExpiresIn = tokenResponse.TryGetValue("expires_in", out var exp) ? exp.GetInt32() : 3600,
            TokenType = tokenResponse.TryGetValue("token_type", out var tt) ? tt.GetString() : "Bearer",
            ExtraProperties = extraProperties.Count > 0 ? extraProperties : null
        };
    }
}

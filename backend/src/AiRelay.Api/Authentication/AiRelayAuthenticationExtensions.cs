namespace AiRelay.Api.Authentication;

public static class AiRelayAuthenticationExtensions
{
    /// <summary>
    /// 尝试从请求头（x-api-key, Authorization）或查询参数（api_key）中提取 API Key
    /// </summary>
    /// <param name="request">HttpRequest 对象</param>
    /// <returns>提取到的 API Key，若未找到则返回 null</returns>
    public static string? GetAiRelayApiKey(this HttpRequest request)
    {
        // 1. 尝试从自定义 Header 获取
        if (request.Headers.TryGetValue(AuthenticationConstants.ApiKeyHeaderName, out var apiKeyHeader))
        {
            var apiKey = apiKeyHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey))
                return apiKey;
        }

        // 2. 尝试从 Authorization Header 获取 Bearer Token
        if (request.Headers.TryGetValue(AuthenticationConstants.AuthorizationHeaderName, out var authHeader))
        {
            var authValue = authHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(authValue) &&
                authValue.StartsWith(AuthenticationConstants.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var apiKey = authValue.Substring(AuthenticationConstants.BearerPrefix.Length).Trim();
                if (!string.IsNullOrEmpty(apiKey))
                    return apiKey;
            }
        }

        // 3. 尝试从 Google API Key Header 获取
        if (request.Headers.TryGetValue(AuthenticationConstants.GoogApiKeyHeaderName, out var googApiKeyHeader))
        {
            var apiKey = googApiKeyHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey))
                return apiKey;
        }

        // 4. 尝试从 Query 参数获取
        if (request.Query.TryGetValue(AuthenticationConstants.ApiKeyQueryParameter, out var queryApiKey))
        {
            var apiKey = queryApiKey.FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey))
                return apiKey;
        }

        return null;
    }
}

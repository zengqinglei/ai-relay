using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Domain.ProviderAccounts.Extensions;

public static class ProviderPlatformExtensions
{
    /// <summary>
    /// 判断是否为 API Key 平台
    /// </summary>
    public static bool IsApiKeyPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.GEMINI_APIKEY
               || platform == ProviderPlatform.CLAUDE_APIKEY
               || platform == ProviderPlatform.OPENAI_APIKEY;
        // Antigravity 不是 API Key 平台，使用 OAuth
    }

    /// <summary>
    /// 判断是否为 Antigravity 平台
    /// </summary>
    public static bool IsAntigravityPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.ANTIGRAVITY;
    }

    /// <summary>
    /// 判断是否为 Gemini 系列平台
    /// </summary>
    public static bool IsGeminiPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.GEMINI_OAUTH
               || platform == ProviderPlatform.GEMINI_APIKEY;
    }

    /// <summary>
    /// 判断是否为 Claude 系列平台
    /// </summary>
    public static bool IsClaudePlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.CLAUDE_OAUTH
               || platform == ProviderPlatform.CLAUDE_APIKEY;
    }

    /// <summary>
    /// 判断是否为 OpenAI 系列平台
    /// </summary>
    public static bool IsOpenAIPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.OPENAI_OAUTH
               || platform == ProviderPlatform.OPENAI_APIKEY;
    }

    /// <summary>
    /// 判断是否为 OAuth 平台 (需要通过授权码交换 Token)
    /// </summary>
    public static bool IsOAuthPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.GEMINI_OAUTH
               || platform == ProviderPlatform.ANTIGRAVITY
               || platform == ProviderPlatform.CLAUDE_OAUTH
               || platform == ProviderPlatform.OPENAI_OAUTH;
    }
}

namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 提供商平台枚举
/// </summary>
public enum ProviderPlatform
{
    GEMINI_OAUTH,
    GEMINI_APIKEY,
    CLAUDE_OAUTH,
    CLAUDE_APIKEY,
    OPENAI_OAUTH,
    OPENAI_APIKEY,

    /// <summary>
    /// Antigravity 平台（统一代理，支持 Gemini 和 Claude 模型）
    /// </summary>
    ANTIGRAVITY
}

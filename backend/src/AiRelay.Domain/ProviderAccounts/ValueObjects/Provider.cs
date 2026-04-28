namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 提供商（决定 Handler、鉴权与 API 协议）
/// </summary>
public enum Provider
{
    Gemini,
    Claude,
    OpenAI,
    Antigravity,
    OpenAICompatible
}

namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 路由配置（外部访问路径 → 可用 Provider+AuthMethod 列表）
/// </summary>
public enum RouteProfile
{
    GeminiInternal,   // /v1internal/**
    GeminiBeta,       // /v1beta/**
    OpenAiResponses,  // /v1/responses/**
    OpenAiCodex,      // /backend-api/codex/**
    ChatCompletions,  // /v1/chat/completions
    ClaudeMessages    // /v1/messages/**
}



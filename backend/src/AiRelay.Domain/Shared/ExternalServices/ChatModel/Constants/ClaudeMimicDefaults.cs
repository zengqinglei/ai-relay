namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Constants;

/// <summary>
/// Claude 官方客户端伪装默认值：Header 配置、系统提示词、第三方指纹清理
/// </summary>
public static class ClaudeMimicDefaults
{
    // ── System Prompts ──────────────────────────────────────────────────────

    /// <summary>Claude Code 官方身份声明</summary>
    public const string ClaudeCodeSystemPrompt = "You are Claude Code, Anthropic's official CLI for Claude.";

    /// <summary>注入到 system 开头的 billing header 文本块</summary>
    public const string BillingHeader = "x-anthropic-billing-header: cc_version=2.1.85.351; cc_entrypoint=cli; cch=00000;";

    /// <summary>OpenCode 第三方指纹（用于清理替换）</summary>
    public const string OpenCodeSystemPrompt = "You are OpenCode, the best coding agent on the planet.";

    // ── anthropic-beta ──────────────────────────────────────────────────────

    public const string AnthropicBeta = "claude-code-20250219,oauth-2025-04-20,interleaved-thinking-2025-05-14";
    public const string AnthropicBetaHaiku = "oauth-2025-04-20,interleaved-thinking-2025-05-14";

    // ── Header 配置（白名单 + 默认值 + 是否强制覆盖）──────────────────────────

    public static readonly Dictionary<string, (bool AllowPassthrough, string? DefaultValue, bool ForceOverride)> Headers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = (true, "application/json", false),
            ["X-Stainless-Retry-Count"] = (true, "0", false),
            ["X-Stainless-Timeout"] = (true, "600", false),
            ["X-Stainless-Lang"] = (true, "js", false),
            ["X-Stainless-Package-Version"] = (true, "0.74.0", false),
            ["X-Stainless-Os"] = (true, "Windows", false),
            ["X-Stainless-Arch"] = (true, "x64", false),
            ["X-Stainless-Runtime"] = (true, "node", false),
            ["X-Stainless-Runtime-Version"] = (true, "v22.17.0", false),
            ["X-Stainless-Helper-Method"] = (true, null, false),
            ["anthropic-dangerous-direct-browser-access"] = (true, "true", false),
            ["anthropic-version"] = (true, "2023-06-01", true),
            ["x-app"] = (true, "cli", true),
            ["anthropic-beta"] = (true, null, true), // 动态设置（根据模型）
            ["accept-language"] = (true, "*", false),
            ["sec-fetch-mode"] = (true, "cors", false),
            ["User-Agent"] = (true, "claude-cli/2.1.85 (external, cli)", true),
            ["content-type"] = (true, null, false)
        };

    public static string GetDefaultValue(string headerKey) =>
        Headers.TryGetValue(headerKey, out var config) ? config.DefaultValue ?? "" : "";
}

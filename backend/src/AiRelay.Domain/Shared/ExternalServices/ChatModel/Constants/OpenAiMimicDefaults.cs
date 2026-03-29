namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Constants;

/// <summary>
/// OpenAI 官方客户端伪装默认值：Header 配置
/// </summary>
public static class OpenAiMimicDefaults
{
    public const string Originator = "codex_cli_rs";
    public const string UserAgent = "codex_cli_rs/0.116.0 (Windows 10.0.26100; x86_64) WindowsTerminal";
    public const string OpenAiBeta = "responses=experimental";

    /// <summary>
    /// Header 配置（AllowPassthrough = 是否允许透传, DefaultValue = 默认值, ForceOverride = 是否强制覆盖）
    /// </summary>
    public static readonly Dictionary<string, (bool AllowPassthrough, string? DefaultValue, bool ForceOverride)> Headers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["accept"] = (true, "application/json", false),
            ["accept-language"] = (true, null, false),
            ["content-type"] = (true, null, false),
            ["conversation_id"] = (true, null, false),
            ["user-agent"] = (true, UserAgent, true),
            ["originator"] = (true, Originator, true), // 强制覆盖
            ["session_id"] = (true, null, false),
            ["x-codex-turn-state"] = (true, null, false),
            ["x-codex-turn-metadata"] = (true, null, false),
            ["openai-beta"] = (true, OpenAiBeta, false)
        };
}

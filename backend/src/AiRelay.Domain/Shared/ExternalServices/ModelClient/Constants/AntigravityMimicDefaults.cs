namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;

/// <summary>
/// Antigravity 官方客户端伪装默认值：Header 配置
/// </summary>
public static class AntigravityMimicDefaults
{
    public const string UserAgent = "antigravity/1.20.6 windows/amd64";
    public const string ContentType = "application/json";

    /// <summary>
    /// Header 配置（AllowPassthrough = 是否允许透传, DefaultValue = 默认值, ForceOverride = 是否强制覆盖）
    /// </summary>
    public static readonly Dictionary<string, (bool AllowPassthrough, string? DefaultValue, bool ForceOverride)> Headers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["User-Agent"] = (false, UserAgent, true),
            ["Content-Type"] = (false, ContentType, true),
            ["anthropic-version"] = (true, null, false),
            ["anthropic-beta"] = (true, null, false)
        };
}

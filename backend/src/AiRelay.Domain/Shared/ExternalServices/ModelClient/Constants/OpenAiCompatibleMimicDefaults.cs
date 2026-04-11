namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;

/// <summary>
/// OpenAI Compatible 官方客户端伪装默认值
/// </summary>
public static class OpenAiCompatibleMimicDefaults
{
    public const string UserAgent = "OpenAI/JS 6.26.0";

    /// <summary>
    /// Header 配置（AllowPassthrough = 是否允许透传, DefaultValue = 默认值, ForceOverride = 是否强制覆盖）
    /// </summary>
    public static readonly Dictionary<string, (bool AllowPassthrough, string? DefaultValue, bool ForceOverride)> Headers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["accept"] = (true, "application/json", false),
            ["accept-language"] = (true, null, false),
            ["content-type"] = (true, null, false),
            ["user-agent"] = (true, UserAgent, false),
            ["x-stainless-lang"] = (true, null, false),
            ["x-stainless-os"] = (true, null, false),
            ["x-stainless-arch"] = (true, null, false),
            ["x-stainless-runtime"] = (true, null, false),
            ["x-stainless-runtime-version"] = (true, null, false),
            ["x-stainless-package-version"] = (true, null, false),
            ["x-stainless-retry-count"] = (true, null, false),
            ["sec-fetch-mode"] = (true, null, false)
        };
}

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;

/// <summary>
/// Gemini 官方客户端伪装默认值：Header 配置
/// </summary>
public static class GeminiMimicDefaults
{
    public const string XGoogApiClientOAuth = "gl-node/22.17.0";
    public const string XGoogApiClientApiKey = "google-genai-sdk/1.30.0 gl-node/v22.17.0";
    public const string UserAgentFormat = "GeminiCLI/0.33.1/{0} (win32; x64) google-api-nodejs-client/10.6.1";
    public const string AcceptLanguage = "*";
    public const string Accept = "*/*";
    public const string SecFetchMode = "cors";

    /// <summary>
    /// Header 白名单配置（AllowPassthrough = 是否允许透传）
    /// </summary>
    public static readonly Dictionary<string, (bool AllowPassthrough, string? DefaultValue, bool ForceOverride)> Headers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = (true, Accept, false),
            ["accept-language"] = (true, AcceptLanguage, false),
            ["sec-fetch-mode"] = (true, SecFetchMode, false),
            ["user-agent"] = (true, null, true), // 强制覆盖（含 modelId，动态生成）
            ["x-goog-api-client"] = (true, null, false), // 动态设置（按平台）
            ["x-gemini-api-privileged-user-id"] = (true, null, false),
            ["Content-Type"] = (true, null, false)
        };
}

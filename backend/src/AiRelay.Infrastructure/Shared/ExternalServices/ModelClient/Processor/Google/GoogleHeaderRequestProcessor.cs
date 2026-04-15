using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;

/// <summary>
/// Google 系平台统一 Header 处理器
/// 覆盖：Antigravity（OAuth）、Gemini OAuth（Code Assist）、Gemini ApiKey（AI Studio）
///
/// 职责：
///   1. 按白名单透传下游合法 Header
///   2. 注入平台认证凭据（Bearer Token / x-goog-api-key）
///   3. 执行官方客户端伪装（Mimic），未检测到真实 CLI 时补充默认 Header
/// </summary>
public class GoogleHeaderRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (options.Provider == Provider.Antigravity)
            ProcessAntigravityHeaders(down, up);
        else
            ProcessGeminiHeaders(down, up);

        return Task.CompletedTask;
    }

    // ── Antigravity ───────────────────────────────────────────────────────────

    private void ProcessAntigravityHeaders(DownRequestContext down, UpRequestContext up)
    {
        // 白名单透传 + 平台默认注入
        foreach (var (key, (allowPassthrough, defaultValue, forceOverride)) in AntigravityMimicDefaults.Headers)
        {
            if (allowPassthrough && down.Headers.TryGetValue(key, out var downValue) && !string.IsNullOrWhiteSpace(downValue))
                up.Headers[key] = downValue;
            else if (defaultValue != null && (forceOverride || !up.Headers.ContainsKey(key)))
                up.Headers[key] = defaultValue;
        }

        up.Headers["Authorization"] = $"Bearer {options.Credential}";
    }

    // ── Gemini ApiKey / Gemini OAuth ─────────────────────────────────────────

    private void ProcessGeminiHeaders(DownRequestContext down, UpRequestContext up)
    {
        // 按白名单透传下游 Header
        foreach (var kvp in down.Headers)
        {
            if (GeminiMimicDefaults.Headers.TryGetValue(kvp.Key, out var config) &&
                config.AllowPassthrough && !string.IsNullOrEmpty(kvp.Value))
            {
                up.Headers[kvp.Key] = kvp.Value;
            }
        }

        // 注入认证 Header
        if (options.AuthMethod == AuthMethod.OAuth)
            up.Headers["Authorization"] = $"Bearer {options.Credential}";
        else
            up.Headers["x-goog-api-key"] = options.Credential;

        // Mimic 伪装：未检测到真实 CLI 时注入默认值
        if (options.ShouldMimicOfficialClient && !IsGeminiCliClient(down))
            InjectGeminiMimicHeaders(up, down);
    }

    private void InjectGeminiMimicHeaders(UpRequestContext up, DownRequestContext down)
    {
        foreach (var (key, (_, defaultValue, forceOverride)) in GeminiMimicDefaults.Headers)
        {
            if (defaultValue == null) continue;
            if (forceOverride || !up.Headers.ContainsKey(key))
                up.Headers[key] = defaultValue;
        }

        // x-goog-api-client 按认证方式区分（OAuth vs ApiKey 值不同）
        if (!up.Headers.ContainsKey("x-goog-api-client"))
        {
            up.Headers["x-goog-api-client"] = options.AuthMethod == AuthMethod.OAuth
                ? GeminiMimicDefaults.XGoogApiClientOAuth
                : GeminiMimicDefaults.XGoogApiClientApiKey;
        }

        // x-gemini-api-privileged-user-id 仅 ApiKey 平台注入
        if (!up.Headers.ContainsKey("x-gemini-api-privileged-user-id") && options.AuthMethod == AuthMethod.ApiKey)
            up.Headers["x-gemini-api-privileged-user-id"] = down.StickySessionId ?? Guid.NewGuid().ToString("D");

        // User-Agent 含 modelId，强制覆盖
        up.Headers["User-Agent"] = string.Format(
            GeminiMimicDefaults.UserAgentFormat,
            up.MappedModelId ?? "gemini-2.0-flash-exp");
    }

    private static bool IsGeminiCliClient(DownRequestContext down)
    {
        var userAgent = down.GetUserAgent();
        return !string.IsNullOrEmpty(userAgent) &&
               userAgent.StartsWith("GeminiCLI/", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-goog-api-client")) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-gemini-api-privileged-user-id"));
    }
}

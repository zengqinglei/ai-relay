using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;

public class GeminiHeaderRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 白名单过滤下游 Headers
        foreach (var kvp in down.Headers)
        {
            if (GeminiMimicDefaults.Headers.TryGetValue(kvp.Key, out var config) &&
                config.AllowPassthrough &&
                !string.IsNullOrEmpty(kvp.Value))
            {
                up.Headers[kvp.Key] = kvp.Value;
            }
        }

        if (options.AuthMethod == AuthMethod.OAuth)
        {
            // 覆盖认证信息
            up.Headers["Authorization"] = $"Bearer {options.Credential}";
        }
        else
        {
            // 认证 Header
            up.Headers["x-goog-api-key"] = options.Credential;
        }

        // 伪装逻辑
        if (options.ShouldMimicOfficialClient)
        {
            bool isOfficialClient = IsGeminiCliClient(down);
            CoverCliHeaders(up, up.MappedModelId, isOfficialClient, down);
        }

        return Task.CompletedTask;
    }

    private static bool IsGeminiCliClient(DownRequestContext down)
    {
        var userAgent = down.GetUserAgent();
        return !string.IsNullOrEmpty(userAgent) &&
               userAgent.StartsWith("GeminiCLI/", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-goog-api-client")) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-gemini-api-privileged-user-id"));
    }

    private void CoverCliHeaders(UpRequestContext up, string? modelId, bool isOfficialClient, DownRequestContext down)
    {
        if (isOfficialClient)
            return; // 官方客户端：身份标识透传，不补充默认值

        // 非官方客户端：遍历配置，补充缺失的静态默认值
        foreach (var (key, (_, defaultValue, forceOverride)) in GeminiMimicDefaults.Headers)
        {
            if (defaultValue == null)
                continue;

            if (forceOverride || !up.Headers.ContainsKey(key))
                up.Headers[key] = defaultValue;
        }

        // x-goog-api-client 动态设置（按平台）
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
        up.Headers["User-Agent"] = string.Format(GeminiMimicDefaults.UserAgentFormat, modelId ?? "gemini-2.0-flash-exp");
    }
}

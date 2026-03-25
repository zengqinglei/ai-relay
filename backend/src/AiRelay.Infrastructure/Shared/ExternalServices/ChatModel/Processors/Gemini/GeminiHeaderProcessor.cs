using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Gemini;

public class GeminiHeaderProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    private static readonly HashSet<string> AllowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept",
        "accept-language",
        "sec-fetch-mode",
        "user-agent",
        "x-goog-api-client",
        "x-gemini-api-privileged-user-id",
        "content-type"
    };


    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 白名单过滤下游 Headers
        foreach (var kvp in down.Headers)
        {
            if (AllowedHeaders.Contains(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                up.Headers[kvp.Key] = kvp.Value;
        }

        if (options.Platform == ProviderPlatform.GEMINI_OAUTH)
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

        if (!up.Headers.ContainsKey("accept"))
            up.Headers["accept"] = "*/*";
        if (!up.Headers.ContainsKey("x-goog-api-client"))
        {
            if (options.Platform == ProviderPlatform.GEMINI_OAUTH)
            {
                up.Headers["x-goog-api-client"] = "gl-node/22.17.0";
            }
            else
            {
                up.Headers["x-goog-api-client"] = "google-genai-sdk/1.30.0 gl-node/v22.17.0";
            }
        }
        if (!up.Headers.ContainsKey("x-gemini-api-privileged-user-id") && options.Platform == ProviderPlatform.GEMINI_APIKEY)
            up.Headers["x-gemini-api-privileged-user-id"] = down.StickySessionId ?? Guid.NewGuid().ToString("D");
        if (!up.Headers.ContainsKey("accept-language"))
            up.Headers["accept-language"] = "*";
        if (!up.Headers.ContainsKey("sec-fetch-mode"))
        // 以下必须覆盖
        if (!isOfficialClient)
        {
            up.Headers["user-agent"] = string.Format("GeminiCLI/0.33.1/{0} (win32; x64) google-api-nodejs-client/10.6.1", modelId ?? "gemini-2.0-flash-exp");
        }
    }
}

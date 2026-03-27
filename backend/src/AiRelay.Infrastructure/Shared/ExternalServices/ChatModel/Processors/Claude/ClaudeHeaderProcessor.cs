using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

/// <summary>
/// Claude OAuth 模式 Header 处理器
/// </summary>
public class ClaudeHeaderProcessor(
    ChatModelConnectionOptions options,
    IClaudeCodeClientDetector clientDetector) : IRequestProcessor
{
    private static readonly HashSet<string> AllowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept",
        "X-Stainless-Retry-Count",
        "X-Stainless-Timeout",
        "X-Stainless-Lang",
        "X-Stainless-Package-Version",
        "X-Stainless-Os",
        "X-Stainless-Arch",
        "X-Stainless-Runtime",
        "X-Stainless-Runtime-Version",
        "X-Stainless-Helper-Method",
        "anthropic-dangerous-direct-browser-access",
        "anthropic-version",
        "x-app",
        "anthropic-beta",
        "accept-language",
        "sec-fetch-mode",
        "User-Agent",
        "content-type"
    };


    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 白名单透传
        foreach (var kvp in down.Headers)
        {
            if (AllowedHeaders.Contains(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                up.Headers[kvp.Key] = kvp.Value;
        }

        if (options.Platform == ProviderPlatform.CLAUDE_OAUTH)
        {
            // 覆盖认证信息
            up.Headers["Authorization"] = $"Bearer {options.Credential}";
        }
        else
        {
            // 移除 OAuth 相关 headers，覆盖认证信息
            up.Headers.Remove("Authorization");
            up.Headers.Remove("cookie");
            up.Headers["x-api-key"] = options.Credential;
        }

        // 伪装官方客户端
        if (options.ShouldMimicOfficialClient)
        {
            bool isOfficialClient = clientDetector.IsClaudeCodeClient(down, down.BodyJsonNode as JsonObject);
            bool isHaikuModel = !string.IsNullOrEmpty(up.MappedModelId) &&
                                up.MappedModelId.Contains("haiku", StringComparison.OrdinalIgnoreCase);
            CoverCliHeaders(up.Headers, isOfficialClient, isHaikuModel);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 覆盖官方 CLI Headers：官方客户端透传已有值，非官方客户端补充缺失的默认值
    /// </summary>
    private static void CoverCliHeaders(Dictionary<string, string> headers, bool isOfficialClient, bool isHaikuModel)
    {
        if (isOfficialClient)
            return; // 官方客户端：身份标识透传，不补充默认值

        // 非官方客户端：缺失则补充默认值
        if (!headers.ContainsKey("Accept"))
            headers["Accept"] = "application/json";
        if (!headers.ContainsKey("X-Stainless-Lang"))
            headers["X-Stainless-Lang"] = "js";
        if (!headers.ContainsKey("X-Stainless-Package-Version"))
            headers["X-Stainless-Package-Version"] = "0.74.0";
        if (!headers.ContainsKey("X-Stainless-Os"))
            headers["X-Stainless-Os"] = "Windows";
        if (!headers.ContainsKey("X-Stainless-Arch"))
            headers["X-Stainless-Arch"] = "x64";
        if (!headers.ContainsKey("X-Stainless-Runtime"))
            headers["X-Stainless-Runtime"] = "node";
        if (!headers.ContainsKey("X-Stainless-Runtime-Version"))
            headers["X-Stainless-Runtime-Version"] = "v22.17.0";
        if (!headers.ContainsKey("X-Stainless-Retry-Count"))
            headers["X-Stainless-Retry-Count"] = "0";
        if (!headers.ContainsKey("X-Stainless-Timeout"))
            headers["X-Stainless-Timeout"] = "600";
        if (!headers.ContainsKey("anthropic-dangerous-direct-browser-access"))
            headers["anthropic-dangerous-direct-browser-access"] = "true";
        if (!headers.ContainsKey("accept-language"))
            headers["accept-language"] = "*";
        if (!headers.ContainsKey("sec-fetch-mode"))
            headers["sec-fetch-mode"] = "cors";

        // 以下必须覆盖
        if (!isOfficialClient)
        {
            headers["User-Agent"] = "claude-cli/2.1.85 (external, cli)";
            headers["x-app"] = "cli";
            headers["anthropic-beta"] = isHaikuModel
                ? "oauth-2025-04-20,interleaved-thinking-2025-05-14"
                : "claude-code-20250219,oauth-2025-04-20,interleaved-thinking-2025-05-14";
            headers["anthropic-version"] = "2023-06-01";
        }
    }
}

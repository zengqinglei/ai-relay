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
        "accept",
        "x-stainless-retry-count",
        "x-stainless-timeout",
        "x-stainless-lang",
        "x-stainless-package-version",
        "x-stainless-os",
        "x-stainless-arch",
        "x-stainless-runtime",
        "x-stainless-runtime-version",
        "x-stainless-helper-method",
        "anthropic-dangerous-direct-browser-access",
        "anthropic-version",
        "x-app",
        "anthropic-beta",
        "accept-language",
        "sec-fetch-mode",
        "user-agent",
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
            up.Headers["authorization"] = $"Bearer {options.Credential}";
        }
        else
        {
            // 移除 OAuth 相关 headers，覆盖认证信息
            up.Headers.Remove("authorization");
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
        if (!headers.ContainsKey("accept"))
            headers["accept"] = "application/json";
        if (!headers.ContainsKey("x-stainless-lang"))
            headers["x-stainless-lang"] = "js";
        if (!headers.ContainsKey("x-stainless-package-version"))
            headers["x-stainless-package-version"] = "0.74.0";
        if (!headers.ContainsKey("x-stainless-os"))
            headers["x-stainless-os"] = "Windows";
        if (!headers.ContainsKey("x-stainless-arch"))
            headers["x-stainless-arch"] = "x64";
        if (!headers.ContainsKey("x-stainless-runtime"))
            headers["x-stainless-runtime"] = "node";
        if (!headers.ContainsKey("x-stainless-runtime-version"))
            headers["x-stainless-runtime-version"] = "v22.17.0";
        if (!headers.ContainsKey("x-stainless-retry-count"))
            headers["x-stainless-retry-count"] = "0";
        if (!headers.ContainsKey("x-stainless-timeout"))
            headers["x-stainless-timeout"] = "600";
        if (!headers.ContainsKey("anthropic-dangerous-direct-browser-access"))
            headers["anthropic-dangerous-direct-browser-access"] = "true";
        if (!headers.ContainsKey("accept-language"))
            headers["accept-language"] = "*";
        if (!headers.ContainsKey("sec-fetch-mode"))
            headers["sec-fetch-mode"] = "cors";

        // 以下必须覆盖
        if (!isOfficialClient)
        {
            headers["user-agent"] = "claude-cli/2.1.81 (external, cli)";
            headers["x-app"] = "cli";
            headers["anthropic-beta"] = isHaikuModel
                ? "oauth-2025-04-20,interleaved-thinking-2025-05-14"
                : "claude-code-20250219,oauth-2025-04-20,interleaved-thinking-2025-05-14";
            headers["anthropic-version"] = "2023-06-01";
        }
    }
}

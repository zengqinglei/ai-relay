using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;

/// <summary>
/// Google 系平台统一 URL 路由处理器
/// 重构后遵循“认证方式决定协议”原则：
///   OAuth  → 走 v1internal 内部协议轨道 (Cloud Code / Antigravity)
///   ApiKey → 走 v1beta 公开协议轨道 (AI Studio)
/// </summary>
public class GoogleUrlRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    private const string CloudCodeBaseUrl = "https://cloudcode-pa.googleapis.com";
    private const string AIStudioBaseUrl  = "https://generativelanguage.googleapis.com";

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var relativePath = NormalizePath(down.RelativePath);
        up.QueryString = down.QueryString;

        if (options.AuthMethod == AuthMethod.OAuth)
        {
            ProcessOAuthInternalProtocol(up, relativePath, down);
        }
        else
        {
            ProcessApiKeyPublicProtocol(up, relativePath);
        }

        InjectSseQueryString(up, down);
        return Task.CompletedTask;
    }

    // ── 内部协议轨道 (OAuth) ──────────────────────────────────────────────────

    private void ProcessOAuthInternalProtocol(UpRequestContext up, string relativePath, DownRequestContext down)
    {
        // 1. 优先级1：如果是显式的 v1internal 接口，始终走内部轨道，无论有没有项目 ID
        if (relativePath.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase))
        {
            up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : CloudCodeBaseUrl;
            up.RelativePath = relativePath;
            return;
        }

        var action = ExtractGoogleAction(relativePath);
        var projectId = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
        bool hasProject = !string.IsNullOrEmpty(projectId);

        // 2. 回退检查：针对【模型调用】，如果是 countTokens 或 缺少项目 ID 且不是 Antigravity 厂商，回退到 AI Studio
        if (options.Provider != Provider.Antigravity && (action == "countTokens" || !hasProject))
        {
            FallbackToPublicProtocol(up, relativePath, down);
            return;
        }

        // 3. 正常内部协议路由逻辑（模型映射）
        up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : CloudCodeBaseUrl;
        
        // 路径自动映射：将 /v1beta/models/...:action 转换为 /v1internal:action
        // 特殊情况：如果是非流式生成且 Provider 为 Gemini，强制映射到 SSE 接口
        if (options.Provider == Provider.Gemini && !down.IsStreaming && action == "generateContent")
        {
            up.RelativePath = "/v1internal:streamGenerateContent";
        }
        else
        {
            up.RelativePath = !string.IsNullOrEmpty(action) ? $"/v1internal:{action}" : relativePath;
        }
    }

    // ── 公开协议轨道 (ApiKey) ─────────────────────────────────────────────────

    private void ProcessApiKeyPublicProtocol(UpRequestContext up, string relativePath)
    {
        up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : AIStudioBaseUrl;
        up.RelativePath = relativePath;
    }

    // ── 公共工具 ──────────────────────────────────────────────────────────────

    private void FallbackToPublicProtocol(UpRequestContext up, string relativePath, DownRequestContext down)
    {
        var action = ExtractGoogleAction(relativePath);
        var modelId = up.MappedModelId ?? down.ModelId ?? "gemini-2.0-flash";

        up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : AIStudioBaseUrl;

        // 如果路径不是以 /v1 开头且包含 action，构造标准 AI Studio 路径
        if (!relativePath.StartsWith("/v1") && !string.IsNullOrEmpty(action))
        {
            up.RelativePath = $"/v1beta/models/{modelId}:{action}";
        }
        else
        {
            up.RelativePath = relativePath;
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        return path.StartsWith('/') ? path : "/" + path;
    }

    private static void InjectSseQueryString(UpRequestContext up, DownRequestContext down)
    {
        bool needsSse = down.IsStreaming ||
                        up.RelativePath.Contains(":streamGenerateContent", StringComparison.OrdinalIgnoreCase);
        if (!needsSse) return;

        if (string.IsNullOrEmpty(up.QueryString))
            up.QueryString = "?alt=sse";
        else if (!up.QueryString.Contains("alt=", StringComparison.OrdinalIgnoreCase))
        {
            var sep = up.QueryString.Contains('?') ? "&" : "?";
            up.QueryString = $"{up.QueryString}{sep}alt=sse";
        }
    }

    private static string ExtractGoogleAction(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        var colonIndex = relativePath.LastIndexOf(':');
        if (colonIndex < 0) return string.Empty;
        var action = relativePath[(colonIndex + 1)..];
        var queryIndex = action.IndexOf('?');
        return queryIndex >= 0 ? action[..queryIndex] : action;
    }
}

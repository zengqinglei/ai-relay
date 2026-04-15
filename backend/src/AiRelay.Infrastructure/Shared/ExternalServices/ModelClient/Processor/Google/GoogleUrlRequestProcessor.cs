using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;

/// <summary>
/// Google 系平台统一 URL 路由处理器
/// 覆盖：Antigravity（OAuth）、Gemini OAuth（Code Assist）、Gemini ApiKey（AI Studio）
///
/// 路由策略：
///   Antigravity   → cloudcode-pa.googleapis.com  + /v1internal:xxx
///   Gemini OAuth  → Branch A/B/C（视 project_id 和 action）
///   Gemini ApiKey → generativelanguage.googleapis.com + /v1beta/models/{model}:{action}
/// </summary>
public class GoogleUrlRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    private const string CloudCodeBaseUrl  = "https://cloudcode-pa.googleapis.com";
    private const string AIStudioBaseUrl   = "https://generativelanguage.googleapis.com";

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var relativePath = down.RelativePath ?? string.Empty;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/'))
            relativePath = "/" + relativePath;

        up.QueryString = down.QueryString;

        if (options.Provider == Provider.Antigravity)
            ProcessAntigravityUrl(up, relativePath);
        else if (options.AuthMethod == AuthMethod.OAuth)
            ProcessGeminiOAuthUrl(up, relativePath, down);
        else
            ProcessGeminiApiKeyUrl(up, relativePath);

        InjectSseQueryString(up, down);
        return Task.CompletedTask;
    }

    // ── Antigravity ───────────────────────────────────────────────────────────

    private void ProcessAntigravityUrl(UpRequestContext up, string relativePath)
    {
        up.BaseUrl    = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : CloudCodeBaseUrl;
        up.RelativePath = relativePath;

        // 强制转换为 /v1internal:xxx（已是 v1internal 时直接透传）
        if (relativePath.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase))
            return;

        var colonIndex = relativePath.LastIndexOf(':');
        if (colonIndex > 0)
        {
            var action = relativePath[(colonIndex + 1)..].Split('?')[0];
            if (!string.IsNullOrEmpty(action))
                up.RelativePath = $"/v1internal:{action}";
        }
    }

    // ── Gemini OAuth（Code Assist）────────────────────────────────────────────

    private void ProcessGeminiOAuthUrl(UpRequestContext up, string relativePath, DownRequestContext down)
    {
        var action      = ExtractGoogleAction(relativePath);
        var projectId   = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
        bool hasProject = !string.IsNullOrEmpty(projectId);

        if (action == "countTokens" || !hasProject)
        {
            // Branch A: AI Studio 直连（countTokens 或无 project_id）
            var modelId = up.MappedModelId ?? down.ModelId ?? "gemini-2.5-flash";
            up.BaseUrl      = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : AIStudioBaseUrl;
            up.RelativePath = $"/v1beta/models/{modelId}:{action}";
        }
        else if (!down.IsStreaming && action == "generateContent")
        {
            // Branch B: Code Assist 非流式 → 强制转 SSE
            up.BaseUrl      = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : CloudCodeBaseUrl;
            up.RelativePath = "/v1internal:streamGenerateContent";
        }
        else
        {
            // Branch C: Code Assist 标准路由
            up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : CloudCodeBaseUrl;
            up.RelativePath = relativePath.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase)
                ? relativePath
                : !string.IsNullOrEmpty(action) ? $"/v1internal:{action}" : relativePath;
        }
    }

    // ── Gemini ApiKey（AI Studio）─────────────────────────────────────────────

    private void ProcessGeminiApiKeyUrl(UpRequestContext up, string relativePath)
    {
        up.BaseUrl      = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : AIStudioBaseUrl;
        up.RelativePath = relativePath;
    }

    // ── 公共工具 ──────────────────────────────────────────────────────────────

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
        var action     = relativePath[(colonIndex + 1)..];
        var queryIndex = action.IndexOf('?');
        return queryIndex >= 0 ? action[..queryIndex] : action;
    }
}

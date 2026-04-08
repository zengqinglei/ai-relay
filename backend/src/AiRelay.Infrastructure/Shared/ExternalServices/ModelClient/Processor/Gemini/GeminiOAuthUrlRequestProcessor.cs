using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;

/// <summary>
/// Gemini OAuth URL routing processor:
/// Routes based on project_id, action, and streaming state
///
/// Branch A: countTokens or no project_id - AI Studio API (/v1beta/models/{model}:{action})
/// Branch B: non-streaming generateContent + has project_id - force upstream SSE (/v1internal:streamGenerateContent?alt=sse)
/// Branch C: standard Code Assist mode (/v1internal:{action})
/// </summary>
public class GeminiOAuthUrlRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    private const string AIStudioBaseUrl = "https://generativelanguage.googleapis.com";
    private const string CodeAssistBaseUrl = "https://cloudcode-pa.googleapis.com";

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var relativePath = down.RelativePath ?? string.Empty;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/'))
            relativePath = "/" + relativePath;
        up.QueryString = down.QueryString;

        var action = ExtractAction(relativePath);

        var projectId = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
        var hasProjectId = !string.IsNullOrEmpty(projectId);

        if (action == "countTokens" || !hasProjectId)
        {
            // Branch A: AI Studio fallback
            var modelId = up.MappedModelId ?? down.ModelId ?? "gemini-2.5-flash";
            up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : AIStudioBaseUrl;
            up.RelativePath = $"/v1beta/models/{modelId}:{action}";
        }
        else if (!down.IsStreaming && action == "generateContent")
        {
            // Branch B: non-streaming Code Assist - force upstream SSE
            up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : CodeAssistBaseUrl;
            up.RelativePath = "/v1internal:streamGenerateContent";
        }
        else
        {
            // Branch C: standard Code Assist (/v1internal:{action})
            up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl) ? options.BaseUrl : CodeAssistBaseUrl;

            if (!relativePath.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(action))
                {
                    up.RelativePath = $"/v1internal:{action}";
                }
                else
                {
                    up.RelativePath = relativePath;
                }
            }
            else
            {
                up.RelativePath = relativePath;
            }
        }

        // alt=sse injection
        bool needsSse = down.IsStreaming ||
                        up.RelativePath.Contains(":streamGenerateContent", StringComparison.OrdinalIgnoreCase);

        if (needsSse)
        {
            if (string.IsNullOrEmpty(up.QueryString))
            {
                up.QueryString = "?alt=sse";
            }
            else if (!up.QueryString.Contains("alt=", StringComparison.OrdinalIgnoreCase))
            {
                var separator = up.QueryString.Contains('?') ? "&" : "?";
                up.QueryString = $"{up.QueryString}{separator}alt=sse";
            }
        }

        return Task.CompletedTask;
    }

    private static string ExtractAction(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;

        var colonIndex = relativePath.LastIndexOf(':');
        if (colonIndex < 0) return string.Empty;

        var action = relativePath[(colonIndex + 1)..];

        var queryIndex = action.IndexOf('?');
        if (queryIndex >= 0) action = action[..queryIndex];

        return action;
    }
}

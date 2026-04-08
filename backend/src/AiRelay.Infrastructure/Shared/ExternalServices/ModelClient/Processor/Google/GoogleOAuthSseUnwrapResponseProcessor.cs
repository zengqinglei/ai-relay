using System.Text;
using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;

/// <summary>
/// OAuth Code Assist SSE response unwrap processor:
/// Strips the {"response": {...}} wrapper from SSE data lines so downstream receives
/// standard Gemini SSE format: data: {"candidates": [...]}
/// 
/// Self-activation: only activates when downstream is streaming AND upstream path contains
/// v1internal (indicating OAuth Code Assist). Otherwise acts as a no-op pass-through.
/// 
/// Ref: sub2api handleNativeStreamingResponse unwrapIfNeeded
/// </summary>
public class GoogleOAuthSseUnwrapResponseProcessor : IResponseProcessor
{
    private readonly bool _isActive;
    public bool RequiresMutation => _isActive;

    public GoogleOAuthSseUnwrapResponseProcessor(bool isDownStreaming, string upRelativePath)
    {
        _isActive = isDownStreaming
            && upRelativePath.Contains("v1internal", StringComparison.OrdinalIgnoreCase);
    }

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (!_isActive) return Task.CompletedTask;
        if (evt.Type == StreamEventType.Error) return Task.CompletedTask;
        if (evt.SseLine == null) return Task.CompletedTask;

        var trimmed = evt.SseLine.Trim();
        if (!trimmed.StartsWith("data:")) return Task.CompletedTask;

        var payload = trimmed[5..].TrimStart();
        if (string.IsNullOrEmpty(payload) || payload == "[DONE]") return Task.CompletedTask;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("response", out var responseObj))
            {
                var unwrapped = responseObj.GetRawText();
                var sseLine = $"data: {unwrapped}\n\n";
                evt.ConvertedBytes = Encoding.UTF8.GetBytes(sseLine);
                evt.SseLine = $"data: {unwrapped}";
            }
        }
        catch
        {
            // JSON parse failure, pass through as-is
        }

        return Task.CompletedTask;
    }
}

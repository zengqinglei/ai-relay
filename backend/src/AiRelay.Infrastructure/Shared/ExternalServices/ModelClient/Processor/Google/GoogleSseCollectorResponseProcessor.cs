using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;

/// <summary>
/// SSE collection and merge processor:
/// When upstream is forced to use SSE streaming (streamGenerateContent?alt=sse) but downstream
/// expects a single JSON response, collects all SSE data lines and merges them into one
/// complete Gemini JSON response.
/// 
/// Self-activation: only activates when downstream is non-streaming AND upstream path contains
/// :streamGenerateContent. Otherwise acts as a no-op pass-through.
/// </summary>
public class GoogleSseCollectorResponseProcessor : IResponseProcessor
{
    private readonly bool _isActive;
    public bool RequiresMutation => _isActive;

    // Collection state (only used when active)
    private readonly List<string> _collectedTextParts = [];
    private string? _lastChunkJson;
    private string? _lastWithPartsJson;
    private ResponseUsage? _lastUsage;

    public GoogleSseCollectorResponseProcessor(bool isDownStreaming, string upRelativePath)
    {
        _isActive = !isDownStreaming
            && upRelativePath.Contains(":streamGenerateContent", StringComparison.OrdinalIgnoreCase);
    }

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (!_isActive) return Task.CompletedTask;
        if (evt.Type == StreamEventType.Error) return Task.CompletedTask;

        if (evt.SseLine != null)
            CollectSseLine(evt);

        return Task.CompletedTask;
    }

    private void CollectSseLine(StreamEvent evt)
    {
        var trimmed = evt.SseLine!.Trim();
        if (!trimmed.StartsWith("data:"))
        {
            evt.OriginalBytes = null;
            evt.ConvertedBytes = null;
            return;
        }

        var json = trimmed[5..].TrimStart();
        if (string.IsNullOrEmpty(json) || json == "[DONE]")
        {
            if (json == "[DONE]")
            {
                // [DONE] 触发 yield break，合成完成事件不会到达 ProcessAsync，
                // 必须在此处完成合并并直接写入当前事件。
                evt.IsComplete = true;
                evt.ConvertedBytes = Encoding.UTF8.GetBytes(BuildMergedJson());
                evt.Usage = _lastUsage;
                evt.Content = string.Concat(_collectedTextParts);
                evt.HasOutput = _collectedTextParts.Count > 0;
            }
            evt.OriginalBytes = null;
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // OAuth unwrap: { "response": {...} } -> {...}
            if (root.TryGetProperty("response", out var responseObj))
            {
                json = responseObj.GetRawText();
                root = responseObj;
            }

            _lastChunkJson = json;

            if (root.TryGetProperty("usageMetadata", out var meta))
                _lastUsage = ExtractUsage(meta);

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var c) &&
                c.TryGetProperty("parts", out var parts))
            {
                _lastWithPartsJson = json;
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                    {
                        var textValue = text.GetString();
                        if (!string.IsNullOrEmpty(textValue))
                            _collectedTextParts.Add(textValue);
                    }
                }
            }
        }
        catch
        {
            // JSON parse failure, skip
        }

        // Suppress forwarding (don't write intermediate SSE chunks to downstream)
        evt.OriginalBytes = null;
        evt.ConvertedBytes = null;
    }

    private string BuildMergedJson()
    {
        var baseJson = _lastWithPartsJson ?? _lastChunkJson ?? "{}";

        if (_collectedTextParts.Count == 0)
            return baseJson;

        var node = JsonNode.Parse(baseJson) as JsonObject;
        if (node == null) return baseJson;

        var mergedText = string.Concat(_collectedTextParts);

        if (node["candidates"] is JsonArray { Count: > 0 } candidates &&
            candidates[0] is JsonObject candidate &&
            candidate["content"] is JsonObject content &&
            content["parts"] is JsonArray parts)
        {
            bool textUpdated = false;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] is JsonObject part && part.ContainsKey("text") && !textUpdated)
                {
                    part["text"] = mergedText;
                    textUpdated = true;
                }
            }
            if (!textUpdated)
            {
                parts.Insert(0, new JsonObject { ["text"] = mergedText });
            }
        }

        return node.ToJsonString();
    }

    private static ResponseUsage ExtractUsage(JsonElement meta)
    {
        int input = 0, output = 0, cached = 0, thoughts = 0;
        if (meta.TryGetProperty("promptTokenCount", out var pt)) input = pt.GetInt32();
        if (meta.TryGetProperty("candidatesTokenCount", out var ct2)) output = ct2.GetInt32();
        if (meta.TryGetProperty("thoughtsTokenCount", out var tt)) thoughts = tt.GetInt32();
        if (meta.TryGetProperty("cachedContentTokenCount", out var cc)) cached = cc.GetInt32();
        return new ResponseUsage(input, output + thoughts, cached);
    }
}

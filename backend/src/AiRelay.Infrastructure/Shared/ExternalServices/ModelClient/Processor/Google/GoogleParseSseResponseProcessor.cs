using System.Text;
using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;

/// <summary>
/// Gemini 平台 SSE 解析 Processor
/// 内聚原 GeminiChatModelResponseParser 的全部逻辑，直接操作 StreamEvent 消除中间对象分配
/// </summary>
public class GoogleParseSseResponseProcessor : IResponseProcessor
{
    public bool RequiresMutation => false;

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (evt.Type == StreamEventType.Error) return Task.CompletedTask;

        if (evt.SseLine != null)
            ParseChunk(evt.SseLine, evt);
        else if (evt.Content != null)
        {
            ParseCompleteResponse(evt.Content, evt);
            evt.IsComplete = true;
        }

        return Task.CompletedTask;
    }

    private static void ParseChunk(string chunk, StreamEvent evt)
    {
        var trimmed = chunk.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("data:")) return;

        var json = trimmed[5..].TrimStart();
        if (string.IsNullOrEmpty(json) || json == "[DONE]") { evt.IsComplete = true; return; }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var responseObj))
                root = responseObj;

            if (root.TryGetProperty("error", out var error))
            {
                evt.Type = StreamEventType.Error;
                evt.Content = ExtractErrorMessage(error);
                return;
            }

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var c) &&
                    c.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    ExtractPartsContent(parts, evt);
                }
            }

            if (root.TryGetProperty("usageMetadata", out var meta)) evt.Usage = ExtractUsage(meta);
        }
        catch { }
    }

    private static void ParseCompleteResponse(string responseBody, StreamEvent evt)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var responseObj))
                root = responseObj;

            if (root.TryGetProperty("error", out var error))
            {
                evt.Type = StreamEventType.Error;
                evt.Content = ExtractErrorMessage(error);
                return;
            }

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var c) &&
                    c.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    ExtractPartsContent(parts, evt);
                }
            }

            if (root.TryGetProperty("usageMetadata", out var meta)) evt.Usage = ExtractUsage(meta);
        }
        catch
        {
            evt.Type = StreamEventType.Error;
            evt.Content = "Invalid JSON response";
        }
    }

    private static void ExtractPartsContent(JsonElement parts, StreamEvent evt)
    {
        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
            {
                var textValue = text.GetString();
                if (!string.IsNullOrEmpty(textValue))
                    sb.Append(textValue);
            }
            else if (part.TryGetProperty("inlineData", out var inline))
            {
                if (inline.TryGetProperty("mimeType", out var mime) &&
                    inline.TryGetProperty("data", out var data))
                {
                    var mimeType = mime.GetString();
                    var dataValue = data.GetString();
                    if (!string.IsNullOrEmpty(mimeType) && !string.IsNullOrEmpty(dataValue))
                        evt.InlineData = new InlineDataPart(mimeType, dataValue);
                }
            }
            else if (part.TryGetProperty("functionCall", out _))
            {
                // 工具调用 part 代表有输出意图
                evt.HasOutput = true;
            }
        }
        if (sb.Length > 0)
        {
            evt.Content = sb.ToString();
            evt.HasOutput = true;
        }
    }

    private static string ExtractErrorMessage(JsonElement error)
    {
        string? errorMsg = null;
        if (error.TryGetProperty("message", out var msg)) errorMsg = msg.GetString();
        if (error.TryGetProperty("code", out var code))
        {
            var codeValue = code.ValueKind == JsonValueKind.Number
                ? code.GetInt32().ToString()
                : code.GetString();
            errorMsg = string.IsNullOrEmpty(errorMsg)
                ? $"Error code: {codeValue}"
                : $"{errorMsg} (code: {codeValue})";
        }
        return errorMsg ?? "Unknown error from upstream";
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

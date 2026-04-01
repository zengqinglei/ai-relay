using System.Text;
using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Response.Google;

/// <summary>
/// Gemini 平台 SSE 解析 Processor
/// 内聚原 GeminiChatModelResponseParser 的全部逻辑
/// </summary>
public class GoogleParseSseResponseProcessor : IResponseProcessor
{
    public bool RequiresMutation => false;

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (evt.Type == StreamEventType.Error) return Task.CompletedTask;

        if (evt.SseLine != null)
        {
            var part = ParseChunk(evt.SseLine);
            if (part != null) ApplyPart(evt, part);
        }
        else if (evt.Content != null)
        {
            var part = ParseCompleteResponse(evt.Content);
            ApplyPart(evt, part);
            evt.IsComplete = true;
        }

        return Task.CompletedTask;
    }

    // ── 迁入自 GeminiChatModelResponseParser ──

    private static ChatResponsePart? ParseChunk(string chunk)
    {
        var trimmed = chunk.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("data:")) return null;

        var json = trimmed[5..].TrimStart();
        if (string.IsNullOrEmpty(json) || json == "[DONE]") return new ChatResponsePart(IsComplete: true);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var responseObj))
                root = responseObj;

            // 检查是否有错误
            if (root.TryGetProperty("error", out var error))
            {
                string? errorMsg = null;
                if (error.TryGetProperty("message", out var msg))
                    errorMsg = msg.GetString();

                if (error.TryGetProperty("code", out var code))
                {
                    var codeValue = code.ValueKind == JsonValueKind.Number
                        ? code.GetInt32().ToString()
                        : code.GetString();
                    errorMsg = string.IsNullOrEmpty(errorMsg)
                        ? $"Error code: {codeValue}"
                        : $"{errorMsg} (code: {codeValue})";
                }

                return new ChatResponsePart(Error: errorMsg ?? "Unknown error from upstream");
            }

            string? content = null;
            InlineDataPart? inlineData = null;
            ResponseUsage? usage = null;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var c) &&
                    c.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
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
                                    inlineData = new InlineDataPart(mimeType, dataValue);
                            }
                        }
                    }
                    if (sb.Length > 0)
                        content = sb.ToString();
                }
            }

            if (root.TryGetProperty("usageMetadata", out var meta))  usage = ExtractUsage(meta);

            return new ChatResponsePart(
                Content: content,
                Usage: usage,
                IsComplete: false,
                InlineData: inlineData
            );
        }
        catch
        {
            return null;
        }
    }

    private static ChatResponsePart ParseCompleteResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var responseObj))
                root = responseObj;

            if (root.TryGetProperty("error", out var error))
            {
                string? errorMsg = null;
                if (error.TryGetProperty("message", out var msg))
                    errorMsg = msg.GetString();

                if (error.TryGetProperty("code", out var code))
                {
                    var codeValue = code.ValueKind == JsonValueKind.Number
                        ? code.GetInt32().ToString()
                        : code.GetString();
                    errorMsg = string.IsNullOrEmpty(errorMsg)
                        ? $"Error code: {codeValue}"
                        : $"{errorMsg} (code: {codeValue})";
                }

                return new ChatResponsePart(Error: errorMsg ?? "Unknown error from upstream", IsComplete: true);
            }

            string? content = null;
            InlineDataPart? inlineData = null;
            ResponseUsage? usage = null;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var c) &&
                    c.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
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
                                    inlineData = new InlineDataPart(mimeType, dataValue);
                            }
                        }
                    }
                    if (sb.Length > 0)
                        content = sb.ToString();
                }
            }

            if (root.TryGetProperty("usageMetadata", out var meta)) usage = ExtractUsage(meta);

            return new ChatResponsePart(
                Content: content,
                Usage: usage,
                IsComplete: true,
                InlineData: inlineData
            );
        }
        catch
        {
            return new ChatResponsePart(Error: "Invalid JSON response");
        }
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

    private static void ApplyPart(StreamEvent evt, ChatResponsePart part)
    {
        if (part.Error != null)
        {
            evt.Type = StreamEventType.Error;
            evt.Content = part.Error;
        }
        else
        {
            evt.Content = part.Content;
        }
        evt.Usage = part.Usage;
        evt.ModelId ??= part.ModelId;
        evt.IsComplete = part.IsComplete;
        evt.InlineData = part.InlineData;
    }
}

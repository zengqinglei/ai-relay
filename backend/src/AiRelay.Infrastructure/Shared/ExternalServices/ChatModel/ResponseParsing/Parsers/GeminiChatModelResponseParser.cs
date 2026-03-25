using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;

public class GeminiChatModelResponseParser : IChatModelResponseParser, IResponseParser
{
    public ChatResponsePart? ParseChunk(string chunk) => ParseChunkStatic(chunk);
    public ChatResponsePart ParseCompleteResponse(string responseBody) => ParseCompleteResponseStatic(responseBody);

    public static ChatResponsePart? ParseChunkStatic(string chunk)
    {
        var trimmed = chunk.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("data:")) return null;

        var json = trimmed.Substring(5).TrimStart();
        if (string.IsNullOrEmpty(json) || json == "[DONE]") return new ChatResponsePart(IsComplete: true);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var responseObj))
            {
                root = responseObj;
            }

            // 检查是否有错误
            if (root.TryGetProperty("error", out var error))
            {
                string? errorMsg = null;
                if (error.TryGetProperty("message", out var msg))
                {
                    errorMsg = msg.GetString();
                }

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
                    // 遍历所有 parts，提取 text 和 inlineData
                    var sb = new System.Text.StringBuilder();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text))
                        {
                            var textValue = text.GetString();
                            if (!string.IsNullOrEmpty(textValue))
                            {
                                sb.Append(textValue);
                            }
                        }
                        else if (part.TryGetProperty("inlineData", out var inline))
                        {
                            if (inline.TryGetProperty("mimeType", out var mime) &&
                                inline.TryGetProperty("data", out var data))
                            {
                                var mimeType = mime.GetString();
                                var dataValue = data.GetString();
                                if (!string.IsNullOrEmpty(mimeType) && !string.IsNullOrEmpty(dataValue))
                                {
                                    inlineData = new InlineDataPart(mimeType, dataValue);
                                }
                            }
                        }
                    }
                    if (sb.Length > 0)
                    {
                        content = sb.ToString();
                    }
                }
            }

            if (root.TryGetProperty("usageMetadata", out var meta))
            {
                int input = 0, output = 0, cached = 0, thoughts = 0;
                if (meta.TryGetProperty("promptTokenCount", out var pt)) input = pt.GetInt32();
                if (meta.TryGetProperty("candidatesTokenCount", out var ct)) output = ct.GetInt32();
                if (meta.TryGetProperty("thoughtsTokenCount", out var tt)) thoughts = tt.GetInt32();
                if (meta.TryGetProperty("cachedContentTokenCount", out var cc)) cached = cc.GetInt32();

                usage = new ResponseUsage(input, output + thoughts, cached);
            }

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

    public static ChatResponsePart ParseCompleteResponseStatic(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var responseObj))
            {
                root = responseObj;
            }

            // 检查是否有错误
            if (root.TryGetProperty("error", out var error))
            {
                string? errorMsg = null;
                if (error.TryGetProperty("message", out var msg))
                {
                    errorMsg = msg.GetString();
                }

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
                    // 遍历所有 parts，提取 text 和 inlineData
                    var sb = new System.Text.StringBuilder();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text))
                        {
                            var textValue = text.GetString();
                            if (!string.IsNullOrEmpty(textValue))
                            {
                                sb.Append(textValue);
                            }
                        }
                        else if (part.TryGetProperty("inlineData", out var inline))
                        {
                            if (inline.TryGetProperty("mimeType", out var mime) &&
                                inline.TryGetProperty("data", out var data))
                            {
                                var mimeType = mime.GetString();
                                var dataValue = data.GetString();
                                if (!string.IsNullOrEmpty(mimeType) && !string.IsNullOrEmpty(dataValue))
                                {
                                    inlineData = new InlineDataPart(mimeType, dataValue);
                                }
                            }
                        }
                    }
                    if (sb.Length > 0)
                    {
                        content = sb.ToString();
                    }
                }
            }

            if (root.TryGetProperty("usageMetadata", out var meta))
            {
                int input = 0, output = 0, cached = 0, thoughts = 0;
                if (meta.TryGetProperty("promptTokenCount", out var pt)) input = pt.GetInt32();
                if (meta.TryGetProperty("candidatesTokenCount", out var ct)) output = ct.GetInt32();
                if (meta.TryGetProperty("thoughtsTokenCount", out var tt)) thoughts = tt.GetInt32();
                if (meta.TryGetProperty("cachedContentTokenCount", out var cc)) cached = cc.GetInt32();

                usage = new ResponseUsage(input, output + thoughts, cached);
            }

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
}

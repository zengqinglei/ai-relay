using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;

public class ClaudeChatModelResponseParser : IChatModelResponseParser, IResponseParser
{
    public ChatResponsePart? ParseChunk(string chunk) => ParseChunkStatic(chunk);
    public ChatResponsePart ParseCompleteResponse(string responseBody) => ParseCompleteResponseStatic(responseBody);

    public static ChatResponsePart? ParseChunkStatic(string chunk)
    {
        var trimmed = chunk.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("data: ")) return null;

        var json = trimmed.Substring(6).Trim();
        if (json == "[DONE]") return new ChatResponsePart(IsComplete: true);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp)) return null;
            var type = typeProp.GetString();

            string? content = null;
            ResponseUsage? usage = null;
            bool isComplete = false;
            string? model = null;

            switch (type)
            {
                case "message_start":
                    if (root.TryGetProperty("message", out var msg))
                    {
                        if (msg.TryGetProperty("model", out var m)) model = m.GetString();
                        if (msg.TryGetProperty("usage", out var u))
                        {
                            int input = 0, cachedRead = 0, cachedCreate = 0;
                            if (u.TryGetProperty("input_tokens", out var it)) input = it.GetInt32();
                            if (u.TryGetProperty("cache_read_input_tokens", out var cr)) cachedRead = cr.GetInt32();
                            if (u.TryGetProperty("cache_creation_input_tokens", out var cc)) cachedCreate = cc.GetInt32();

                            usage = new ResponseUsage(input, 0, cachedRead, cachedCreate);
                        }
                    }
                    break;

                case "content_block_delta":
                    if (root.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("type", out var deltaType) &&
                        deltaType.GetString() == "text_delta" &&
                        delta.TryGetProperty("text", out var text))
                    {
                        content = text.GetString();
                    }
                    break;

                case "message_delta":
                    if (root.TryGetProperty("usage", out var deltaUsage) &&
                        deltaUsage.TryGetProperty("output_tokens", out var ot))
                    {
                        usage = new ResponseUsage(0, ot.GetInt32());
                    }
                    break;

                case "message_stop":
                    isComplete = true;
                    break;

                case "error":
                    // Claude 错误事件
                    string? errorMsg = null;
                    if (root.TryGetProperty("error", out var error))
                    {
                        if (error.TryGetProperty("message", out var errMsg))
                        {
                            errorMsg = errMsg.GetString();
                        }

                        if (error.TryGetProperty("type", out var errType))
                        {
                            var typeStr = errType.GetString();
                            errorMsg = string.IsNullOrEmpty(errorMsg)
                                ? $"Error type: {typeStr}"
                                : $"{errorMsg} (type: {typeStr})";
                        }
                    }
                    return new ChatResponsePart(Error: errorMsg ?? "Unknown error from upstream");
            }

            if (content == null && usage == null && !isComplete && model == null) return null;

            return new ChatResponsePart(
                Content: content,
                Usage: usage,
                IsComplete: isComplete,
                ModelId: model
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

            string? content = null;
            string? model = null;
            ResponseUsage? usage = null;

            if (root.TryGetProperty("model", out var m)) model = m.GetString();

            if (root.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
            {
                foreach (var block in contentArray.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                        block.TryGetProperty("text", out var text))
                    {
                        content += text.GetString();
                    }
                }
            }

            if (root.TryGetProperty("usage", out var u))
            {
                int input = 0, output = 0, cachedRead = 0, cachedCreate = 0;
                if (u.TryGetProperty("input_tokens", out var it)) input = it.GetInt32();
                if (u.TryGetProperty("output_tokens", out var ot)) output = ot.GetInt32();
                if (u.TryGetProperty("cache_read_input_tokens", out var cr)) cachedRead = cr.GetInt32();
                if (u.TryGetProperty("cache_creation_input_tokens", out var cc)) cachedCreate = cc.GetInt32();

                usage = new ResponseUsage(input, output, cachedRead, cachedCreate);
            }

            return new ChatResponsePart(
                Content: content,
                Usage: usage,
                IsComplete: true,
                ModelId: model
            );
        }
        catch
        {
            return new ChatResponsePart(Error: "Invalid JSON response");
        }
    }
}

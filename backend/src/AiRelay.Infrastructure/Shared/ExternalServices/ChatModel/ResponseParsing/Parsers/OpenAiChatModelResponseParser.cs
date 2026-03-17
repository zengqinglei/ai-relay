using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;

public class OpenAiChatModelResponseParser : IChatModelResponseParser, IResponseParser
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

            string? content = null;
            string? model = null;
            ResponseUsage? usage = null;
            bool isComplete = false;

            // 检测是 Responses API 还是 Chat Completions API
            if (root.TryGetProperty("type", out var typeProperty))
            {
                var eventType = typeProperty.GetString();

                // Responses API 格式
                switch (eventType)
                {
                    case "response.output_text.delta":
                        // 文本增量事件
                        if (root.TryGetProperty("delta", out var delta))
                        {
                            content = delta.GetString();
                        }
                        break;

                    case "response.completed":
                        // 响应完成事件
                        isComplete = true;

                        // 提取 usage
                        if (root.TryGetProperty("response", out var response))
                        {
                            if (response.TryGetProperty("model", out var m))
                            {
                                model = m.GetString();
                            }

                            if (response.TryGetProperty("usage", out var u))
                            {
                                usage = ExtractResponsesApiUsage(u);
                            }
                        }
                        break;

                    default:
                        // 其他事件类型（如 response.created, response.in_progress 等）忽略
                        return null;
                }
            }
            else
            {
                // Chat Completions API 格式（原有逻辑）
                if (root.TryGetProperty("model", out var modelProp))
                {
                    model = modelProp.GetString();
                }

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var c))
                    {
                        content = c.GetString();
                    }
                }

                if (root.TryGetProperty("usage", out var u))
                {
                    usage = ExtractUsage(u);
                }
            }

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

            if (root.TryGetProperty("model", out var modelProp))
            {
                model = modelProp.GetString();
            }

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var c))
                {
                    content = c.GetString();
                }
            }

            if (root.TryGetProperty("usage", out var u))
            {
                usage = ExtractUsage(u);
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

    private static ResponseUsage ExtractUsage(JsonElement usageElement)
    {
        int input = 0, output = 0, cached = 0;

        if (usageElement.TryGetProperty("prompt_tokens", out var pt)) input = pt.GetInt32();
        if (usageElement.TryGetProperty("completion_tokens", out var ct)) output = ct.GetInt32();

        if (usageElement.TryGetProperty("prompt_tokens_details", out var details))
        {
            if (details.TryGetProperty("cached_tokens", out var c)) cached = c.GetInt32();
        }

        return new ResponseUsage(input, output, cached);
    }

    private static ResponseUsage ExtractResponsesApiUsage(JsonElement usageElement)
    {
        int input = 0, output = 0, cached = 0;

        // Responses API 使用 input_tokens 和 output_tokens
        if (usageElement.TryGetProperty("input_tokens", out var it)) input = it.GetInt32();
        if (usageElement.TryGetProperty("output_tokens", out var ot)) output = ot.GetInt32();

        if (usageElement.TryGetProperty("input_tokens_details", out var details))
        {
            if (details.TryGetProperty("cached_tokens", out var c)) cached = c.GetInt32();
        }

        return new ResponseUsage(input, output, cached);
    }
}

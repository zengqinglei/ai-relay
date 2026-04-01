using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Response.OpenAi;

/// <summary>
/// OpenAI 平台 SSE 解析 Processor
/// 内聚原 OpenAiChatModelResponseParser 的全部逻辑
/// </summary>
public class OpenAiParseSseResponseProcessor : IResponseProcessor
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

    // ── 迁入自 OpenAiChatModelResponseParser ──

    private static ChatResponsePart? ParseChunk(string chunk)
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

            if (root.TryGetProperty("type", out var typeProperty))
            {
                var eventType = typeProperty.GetString();

                switch (eventType)
                {
                    case "response.output_text.delta":
                        if (root.TryGetProperty("delta", out var delta))
                            content = delta.GetString();
                        break;

                    case "response.completed":
                        isComplete = true;
                        if (root.TryGetProperty("response", out var response))
                        {
                            if (response.TryGetProperty("model", out var m))
                                model = m.GetString();
                            if (response.TryGetProperty("usage", out var u))
                                usage = ExtractResponsesApiUsage(u);
                        }
                        break;

                    case "response.failed":
                        string? errorMsg = null;
                        if (root.TryGetProperty("response", out var failedResponse) &&
                            failedResponse.TryGetProperty("error", out var error))
                        {
                            if (error.TryGetProperty("message", out var msg))
                                errorMsg = msg.GetString();
                            if (error.TryGetProperty("code", out var code))
                            {
                                var codeStr = code.GetString();
                                errorMsg = string.IsNullOrEmpty(errorMsg)
                                    ? $"Error code: {codeStr}"
                                    : $"{errorMsg} (code: {codeStr})";
                            }
                        }
                        return new ChatResponsePart(Error: errorMsg ?? "Unknown error from upstream");

                    default:
                        return null;
                }
            }
            else
            {
                // Chat Completions API 格式
                if (root.TryGetProperty("model", out var modelProp))
                    model = modelProp.GetString();

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
                    usage = ExtractUsage(u);
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

    private static ChatResponsePart ParseCompleteResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string? content = null;
            string? model = null;
            ResponseUsage? usage = null;

            if (root.TryGetProperty("model", out var modelProp))
                model = modelProp.GetString();

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
                usage = ExtractUsage(u);

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
        if (usageElement.TryGetProperty("input_tokens", out var it)) input = it.GetInt32();
        if (usageElement.TryGetProperty("output_tokens", out var ot)) output = ot.GetInt32();
        if (usageElement.TryGetProperty("input_tokens_details", out var details))
        {
            if (details.TryGetProperty("cached_tokens", out var c)) cached = c.GetInt32();
        }
        return new ResponseUsage(input, output, cached);
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

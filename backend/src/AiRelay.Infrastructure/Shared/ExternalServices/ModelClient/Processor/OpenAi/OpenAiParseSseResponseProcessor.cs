using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi;

/// <summary>
/// OpenAI 平台 SSE 解析 Processor
/// 内聚原 OpenAiChatModelResponseParser 的全部逻辑，直接操作 StreamEvent 消除中间对象分配
/// </summary>
public class OpenAiParseSseResponseProcessor : IResponseProcessor
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
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("data: ")) return;

        var json = trimmed[6..].Trim();
        if (json == "[DONE]") { evt.IsComplete = true; return; }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeProperty))
            {
                // Responses API 格式
                switch (typeProperty.GetString())
                {
                    case "response.output_text.delta":
                        if (root.TryGetProperty("delta", out var delta))
                        {
                            var text = delta.GetString();
                            evt.Content = text;
                            if (!string.IsNullOrEmpty(text)) evt.HasOutput = true;
                        }
                        break;

                    case "response.completed":
                        evt.IsComplete = true;
                        if (root.TryGetProperty("response", out var response))
                        {
                            if (response.TryGetProperty("model", out var m)) evt.ModelId ??= m.GetString();
                            if (response.TryGetProperty("usage", out var u)) evt.Usage = ExtractResponsesApiUsage(u);
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
                        evt.Type = StreamEventType.Error;
                        evt.Content = errorMsg ?? "Unknown error from upstream";
                        break;
                }
            }
            else
            {
                // Chat Completions API 格式
                if (root.TryGetProperty("model", out var modelProp)) evt.ModelId ??= modelProp.GetString();

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("delta", out var delta))
                    {
                        if (delta.TryGetProperty("content", out var c))
                        {
                            var text = c.GetString();
                            evt.Content = text;
                            if (!string.IsNullOrEmpty(text)) evt.HasOutput = true;
                        }
                        
                        if (delta.TryGetProperty("tool_calls", out _) || delta.TryGetProperty("reasoning_content", out _))
                        {
                            evt.HasOutput = true;
                        }
                    }
                }
                else if (root.TryGetProperty("error", out var errorProp))
                {
                    evt.Type = StreamEventType.Error;
                    
                    string? errorMsg = null;
                    if (errorProp.TryGetProperty("message", out var msg))
                    {
                        errorMsg = msg.GetString();
                    }
                    evt.Content = errorMsg ?? errorProp.ToString();
                }

                if (root.TryGetProperty("usage", out var u)) evt.Usage = ExtractUsage(u);
            }
        }
        catch { }
    }

    private static void ParseCompleteResponse(string responseBody, StreamEvent evt)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("model", out var modelProp)) evt.ModelId ??= modelProp.GetString();

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var c))
                {
                    evt.Content = c.GetString();
                }
            }

            if (root.TryGetProperty("usage", out var u)) evt.Usage = ExtractUsage(u);
        }
        catch
        {
            evt.Type = StreamEventType.Error;
            evt.Content = "Invalid JSON response";
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
}

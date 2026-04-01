using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Claude;

/// <summary>
/// Claude 平台 SSE 解析 Processor
/// 内聚原 ClaudeChatModelResponseParser 的全部逻辑，直接操作 StreamEvent 消除中间对象分配
/// </summary>
public class ClaudeParseSseResponseProcessor : IResponseProcessor
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

            if (!root.TryGetProperty("type", out var typeProp)) return;

            switch (typeProp.GetString())
            {
                case "message_start":
                    if (root.TryGetProperty("message", out var msg))
                    {
                        if (msg.TryGetProperty("model", out var m)) evt.ModelId ??= m.GetString();
                        if (msg.TryGetProperty("usage", out var u)) evt.Usage = ExtractUsage(u);
                    }
                    break;

                case "content_block_delta":
                    if (root.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("type", out var deltaType) &&
                        deltaType.GetString() == "text_delta" &&
                        delta.TryGetProperty("text", out var text))
                    {
                        evt.Content = text.GetString();
                    }
                    break;

                case "message_delta":
                    if (root.TryGetProperty("usage", out var deltaUsage))
                        evt.Usage = ExtractUsage(deltaUsage);
                    break;

                case "message_stop":
                    evt.IsComplete = true;
                    break;

                case "error":
                    string? errorMsg = null;
                    if (root.TryGetProperty("error", out var error))
                    {
                        if (error.TryGetProperty("message", out var errMsg))
                            errorMsg = errMsg.GetString();
                        if (error.TryGetProperty("type", out var errType))
                        {
                            var typeStr = errType.GetString();
                            errorMsg = string.IsNullOrEmpty(errorMsg)
                                ? $"Error type: {typeStr}"
                                : $"{errorMsg} (type: {typeStr})";
                        }
                    }
                    evt.Type = StreamEventType.Error;
                    evt.Content = errorMsg ?? "Unknown error from upstream";
                    break;
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

            if (root.TryGetProperty("model", out var m)) evt.ModelId ??= m.GetString();

            if (root.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
            {
                string? content = null;
                foreach (var block in contentArray.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                        block.TryGetProperty("text", out var text))
                    {
                        content += text.GetString();
                    }
                }
                evt.Content = content;
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
        int input = 0, output = 0, cachedRead = 0, cachedCreate = 0;
        if (usageElement.TryGetProperty("input_tokens", out var it)) input = it.GetInt32();
        if (usageElement.TryGetProperty("output_tokens", out var ot)) output = ot.GetInt32();
        if (usageElement.TryGetProperty("cache_read_input_tokens", out var cr)) cachedRead = cr.GetInt32();
        if (usageElement.TryGetProperty("cache_creation_input_tokens", out var cc)) cachedCreate = cc.GetInt32();

        return new ResponseUsage(input, output, cachedRead, cachedCreate);
    }
}

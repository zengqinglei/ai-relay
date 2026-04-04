using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi.Converter;

/// <summary>
/// Responses API SSE → Chat Completions SSE 响应转换器（有状态，每个请求创建一个实例）
/// </summary>
public class ResponsesToCompletionsConverter(bool includeUsage = false)
{
    private string _id = GenerateChatCmplId();
    private string _model = string.Empty;
    private long _created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private bool _sentRole;
    private bool _sawToolCall;
    private bool _finalized;
    private int _nextToolCallIndex;

    // Responses output_index → Chat tool_calls index
    private readonly Dictionary<int, int> _outputIndexToToolIndex = new();

    public IEnumerable<string> ConvertSseLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("data: ")) yield break;

        var json = trimmed[6..].Trim();
        if (json == "[DONE]")
        {
            // 若上游直接发 [DONE] 而没有 response.completed，补发 finish chunk
            foreach (var chunk in Finalize())
                yield return FormatChunk(chunk);
            yield return "data: [DONE]";
            yield break;
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProperty)) yield break;
            var eventType = typeProperty.GetString();

            switch (eventType)
            {
                case "response.created":
                    foreach (var c in HandleCreated(root)) yield return FormatChunk(c);
                    break;

                case "response.output_text.delta":
                    foreach (var c in HandleTextDelta(root)) yield return FormatChunk(c);
                    break;

                case "response.output_item.added":
                    foreach (var c in HandleOutputItemAdded(root)) yield return FormatChunk(c);
                    break;

                case "response.function_call_arguments.delta":
                    foreach (var c in HandleFuncArgsDelta(root)) yield return FormatChunk(c);
                    break;

                case "response.reasoning_summary_text.delta":
                    foreach (var c in HandleReasoningDelta(root)) yield return FormatChunk(c);
                    break;

                case "response.completed":
                case "response.incomplete":
                case "response.failed":
                    foreach (var c in HandleCompleted(root)) yield return FormatChunk(c);
                    yield return "data: [DONE]";
                    break;
            }
        }
        finally
        {
            doc?.Dispose();
        }
    }

    // ── Event handlers ──

    private IEnumerable<JsonObject> HandleCreated(JsonElement root)
    {
        if (root.TryGetProperty("response", out var resp))
        {
            if (resp.TryGetProperty("id", out var id) && id.GetString() is { } idStr && idStr != "")
                _id = idStr;
            if (resp.TryGetProperty("model", out var m) && m.GetString() is { } mStr && mStr != "")
                _model = mStr;
        }

        if (_sentRole) yield break;
        _sentRole = true;

        yield return MakeDeltaChunk(new JsonObject { ["role"] = "assistant" });
    }

    private IEnumerable<JsonObject> HandleTextDelta(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var delta)) yield break;
        var content = delta.GetString();
        if (string.IsNullOrEmpty(content)) yield break;

        yield return MakeDeltaChunk(new JsonObject { ["content"] = content });
    }

    private IEnumerable<JsonObject> HandleReasoningDelta(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var delta)) yield break;
        var content = delta.GetString();
        if (string.IsNullOrEmpty(content)) yield break;

        yield return MakeDeltaChunk(new JsonObject { ["reasoning_content"] = content });
    }

    private IEnumerable<JsonObject> HandleOutputItemAdded(JsonElement root)
    {
        if (!root.TryGetProperty("item", out var item)) yield break;
        if (!item.TryGetProperty("type", out var itemType) || itemType.GetString() != "function_call") yield break;

        var outputIndex = root.TryGetProperty("output_index", out var oi) ? oi.GetInt32() : 0;
        var toolIndex = _nextToolCallIndex++;
        _outputIndexToToolIndex[outputIndex] = toolIndex;
        _sawToolCall = true;

        var callId = item.TryGetProperty("call_id", out var cid) ? cid.GetString() : null;
        var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;

        // 只输出 id 和 name，arguments 通过后续 delta 事件推送
        var toolCallDelta = new JsonObject
        {
            ["index"] = toolIndex,
            ["id"] = callId,
            ["type"] = "function",
            ["function"] = new JsonObject { ["name"] = name, ["arguments"] = "" }
        };

        yield return MakeDeltaChunk(new JsonObject
        {
            ["tool_calls"] = new JsonArray { toolCallDelta }
        });
    }

    private IEnumerable<JsonObject> HandleFuncArgsDelta(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var delta)) yield break;
        var argsDelta = delta.GetString();
        if (string.IsNullOrEmpty(argsDelta)) yield break;

        var outputIndex = root.TryGetProperty("output_index", out var oi) ? oi.GetInt32() : 0;
        if (!_outputIndexToToolIndex.TryGetValue(outputIndex, out var toolIndex)) yield break;

        var toolCallDelta = new JsonObject
        {
            ["index"] = toolIndex,
            ["function"] = new JsonObject { ["arguments"] = argsDelta }
        };

        yield return MakeDeltaChunk(new JsonObject
        {
            ["tool_calls"] = new JsonArray { toolCallDelta }
        });
    }

    private IEnumerable<JsonObject> HandleCompleted(JsonElement root)
    {
        _finalized = true;

        var finishReason = _sawToolCall ? "tool_calls" : "stop";
        ChatUsage? usage = null;
        string? model = null;

        if (root.TryGetProperty("response", out var resp))
        {
            if (resp.TryGetProperty("model", out var m)) model = m.GetString();

            // incomplete → length
            if (resp.TryGetProperty("status", out var status) && status.GetString() == "incomplete")
            {
                if (resp.TryGetProperty("incomplete_details", out var details) &&
                    details.TryGetProperty("reason", out var reason) &&
                    reason.GetString() == "max_output_tokens")
                {
                    finishReason = "length";
                }
                else
                {
                    finishReason = "stop";
                }
            }

            if (resp.TryGetProperty("usage", out var u))
                usage = ExtractUsage(u);
        }

        if (!string.IsNullOrEmpty(model)) _model = model!;

        // finish chunk
        yield return MakeFinishChunk(finishReason);

        // usage chunk（空 choices）—— 仅当客户端请求了 stream_options.include_usage=true 时发送
        if (includeUsage && usage != null)
        {
            yield return new JsonObject
            {
                ["id"] = _id,
                ["object"] = "chat.completion.chunk",
                ["created"] = _created,
                ["model"] = _model,
                ["choices"] = new JsonArray(),
                ["usage"] = new JsonObject
                {
                    ["prompt_tokens"] = usage.PromptTokens,
                    ["completion_tokens"] = usage.CompletionTokens,
                    ["total_tokens"] = usage.PromptTokens + usage.CompletionTokens,
                    ["prompt_tokens_details"] = new JsonObject
                    {
                        ["cached_tokens"] = usage.CachedTokens
                    }
                }
            };
        }
    }

    public bool IsFinalized => _finalized;

    public IEnumerable<JsonObject> Finalize()
    {
        if (_finalized) yield break;
        _finalized = true;
        yield return MakeFinishChunk(_sawToolCall ? "tool_calls" : "stop");
    }

    // ── Helpers ──

    private JsonObject MakeDeltaChunk(JsonObject delta)
    {
        return new JsonObject
        {
            ["id"] = _id,
            ["object"] = "chat.completion.chunk",
            ["created"] = _created,
            ["model"] = _model,
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = delta,
                    ["finish_reason"] = JsonValue.Create<string?>(null)
                }
            }
        };
    }

    private JsonObject MakeFinishChunk(string finishReason)
    {
        return new JsonObject
        {
            ["id"] = _id,
            ["object"] = "chat.completion.chunk",
            ["created"] = _created,
            ["model"] = _model,
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject { ["content"] = "" },
                    ["finish_reason"] = finishReason
                }
            }
        };
    }

    private static string FormatChunk(JsonObject chunk) => $"data: {chunk.ToJsonString()}";

    private static string GenerateChatCmplId()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return "chatcmpl-" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static ChatUsage ExtractUsage(JsonElement u)
    {
        var input = u.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
        var output = u.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
        var cached = 0;
        if (u.TryGetProperty("input_tokens_details", out var details) &&
            details.TryGetProperty("cached_tokens", out var ct))
            cached = ct.GetInt32();
        return new ChatUsage(input, output, cached);
    }

    private record ChatUsage(int PromptTokens, int CompletionTokens, int CachedTokens);
}

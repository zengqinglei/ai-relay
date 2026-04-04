using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi;

/// <summary>
/// 非流式下游 + Responses API SSE 上游的缓冲处理器。
/// 参考 sub2api handleChatBufferedStreamingResponse 策略：
/// 逐行消费 Responses API SSE，从 response.completed 提取终态，
/// 在流结束时拼装单个 Chat Completions JSON 响应写入 ConvertedBytes。
/// </summary>
public class OpenAiBufferedChatResponseProcessor : IResponseProcessor
{
    private string _id = GenerateChatCmplId();
    private readonly long _created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private string _model = string.Empty;
    private readonly StringBuilder _content = new();
    private bool _finalized;

    private int _inputTokens;
    private int _outputTokens;
    private int _cachedTokens;

    private string _responseStatus = string.Empty;
    private string _incompleteReason = string.Empty;
    private bool _sawToolCalls;

    // tool_calls 积累（流式 SSE 路径）
    private readonly List<(string CallId, string Name, StringBuilder Arguments)> _toolCallsBuffer = new();
    private readonly Dictionary<int, int> _outputIndexToToolCallIndex = new();

    public bool RequiresMutation => true;

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (evt.Type == StreamEventType.Error) return Task.CompletedTask;

        // 流式路径：每行在 evt.SseLine 中
        // 非流式路径：整个响应体在 evt.Content 中，IsComplete=true
        if (evt.SseLine != null)
            ProcessSseLine(evt.SseLine, evt);
        else if (evt.Content != null && evt.IsComplete)
            ProcessFullBody(evt.Content, evt);

        return Task.CompletedTask;
    }

    private void ProcessSseLine(string line, StreamEvent evt)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("data: "))
        {
            // event: 行及空行——抑制透传
            evt.OriginalBytes = null;
            evt.ConvertedBytes = null;
            return;
        }

        var json = trimmed[6..].Trim();
        if (json == "[DONE]")
        {
            WriteAssembledResponse(evt);
            return;
        }

        // Mutation 模式：抑制所有中间行透传，仅在完成时输出单个 JSON
        evt.OriginalBytes = null;
        evt.ConvertedBytes = null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp)) return;

            switch (typeProp.GetString())
            {
                case "response.created":
                    if (root.TryGetProperty("response", out var created))
                    {
                        if (created.TryGetProperty("id", out var id) && id.GetString() is { } idStr && idStr != "")
                            _id = idStr;
                        if (created.TryGetProperty("model", out var m) && m.GetString() is { } mStr && mStr != "")
                            _model = mStr;
                    }
                    break;

                case "response.output_text.delta":
                    if (root.TryGetProperty("delta", out var delta) && delta.GetString() is { } text)
                        _content.Append(text);
                    break;

                case "response.output_item.added":
                    if (root.TryGetProperty("item", out var item) &&
                        item.TryGetProperty("type", out var itemType) && itemType.GetString() == "function_call")
                    {
                        var callId = item.TryGetProperty("call_id", out var cid) ? cid.GetString() ?? "" : "";
                        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var outputIndex = root.TryGetProperty("output_index", out var oi) ? oi.GetInt32() : 0;
                        _outputIndexToToolCallIndex[outputIndex] = _toolCallsBuffer.Count;
                        _toolCallsBuffer.Add((callId, name, new StringBuilder()));
                        _sawToolCalls = true;
                    }
                    break;

                case "response.function_call_arguments.delta":
                    if (root.TryGetProperty("delta", out var argsDelta) && argsDelta.GetString() is { } argsText)
                    {
                        var outputIndex = root.TryGetProperty("output_index", out var oi2) ? oi2.GetInt32() : 0;
                        if (_outputIndexToToolCallIndex.TryGetValue(outputIndex, out var tcIdx))
                            _toolCallsBuffer[tcIdx].Arguments.Append(argsText);
                    }
                    break;

                case "response.completed":
                case "response.incomplete":
                case "response.failed":
                    ExtractCompletedFields(root);
                    WriteAssembledResponse(evt);
                    break;
            }
        }
        catch { }
    }

    private void ProcessFullBody(string body, StreamEvent evt)
    {
        // 非流式路径：body 为 SSE 文本，逐行解析
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data: ") || trimmed == "data: [DONE]") continue;
            var json = trimmed[6..].Trim();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp)) continue;

                switch (typeProp.GetString())
                {
                    case "response.output_text.delta":
                        if (root.TryGetProperty("delta", out var delta) && delta.GetString() is { } text)
                            _content.Append(text);
                        break;

                    case "response.output_item.added":
                        if (root.TryGetProperty("item", out var item) &&
                            item.TryGetProperty("type", out var itemType) && itemType.GetString() == "function_call")
                        {
                            var callId = item.TryGetProperty("call_id", out var cid) ? cid.GetString() ?? "" : "";
                            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            var outputIndex = root.TryGetProperty("output_index", out var oi) ? oi.GetInt32() : 0;
                            _outputIndexToToolCallIndex[outputIndex] = _toolCallsBuffer.Count;
                            _toolCallsBuffer.Add((callId, name, new StringBuilder()));
                            _sawToolCalls = true;
                        }
                        break;

                    case "response.function_call_arguments.delta":
                        if (root.TryGetProperty("delta", out var argsDelta) && argsDelta.GetString() is { } argsText)
                        {
                            var outputIndex = root.TryGetProperty("output_index", out var oi2) ? oi2.GetInt32() : 0;
                            if (_outputIndexToToolCallIndex.TryGetValue(outputIndex, out var tcIdx))
                                _toolCallsBuffer[tcIdx].Arguments.Append(argsText);
                        }
                        break;

                    case "response.completed":
                    case "response.incomplete":
                    case "response.failed":
                        ExtractCompletedFields(root);
                        break;
                }
            }
            catch { }
        }

        WriteAssembledResponse(evt);
    }

    private void ExtractCompletedFields(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var resp)) return;

        if (resp.TryGetProperty("model", out var m) && m.GetString() is { } mStr && mStr != "")
            _model = mStr;

        if (resp.TryGetProperty("status", out var status) && status.GetString() is { } statusStr)
            _responseStatus = statusStr;

        if (resp.TryGetProperty("incomplete_details", out var incompleteDetails) &&
            incompleteDetails.TryGetProperty("reason", out var incompleteReason) &&
            incompleteReason.GetString() is { } reasonStr)
            _incompleteReason = reasonStr;

        if (resp.TryGetProperty("usage", out var u))
        {
            if (u.TryGetProperty("input_tokens", out var it)) _inputTokens = it.GetInt32();
            if (u.TryGetProperty("output_tokens", out var ot)) _outputTokens = ot.GetInt32();
            if (u.TryGetProperty("input_tokens_details", out var details) &&
                details.TryGetProperty("cached_tokens", out var ct))
                _cachedTokens = ct.GetInt32();
        }

        // 非流式上游返回完整 response 时，从 output[].content 提取文本；同时检测 function_call
        if (resp.TryGetProperty("output", out var outputs))
        {
            foreach (var output in outputs.EnumerateArray())
            {
                if (output.TryGetProperty("type", out var outputType) && outputType.GetString() == "function_call")
                {
                    _sawToolCalls = true;
                    var callId = output.TryGetProperty("call_id", out var cid) ? cid.GetString() ?? "" : "";
                    var name = output.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var arguments = output.TryGetProperty("arguments", out var a) ? a.GetString() ?? "" : "";
                    _toolCallsBuffer.Add((callId, name, new StringBuilder(arguments)));
                    continue;
                }
                if (_content.Length == 0 && output.TryGetProperty("content", out var contentArr))
                {
                    foreach (var part in contentArr.EnumerateArray())
                    {
                        if (part.TryGetProperty("type", out var t) && t.GetString() == "output_text" &&
                            part.TryGetProperty("text", out var txt) && txt.GetString() is { } s)
                            _content.Append(s);
                    }
                }
            }
        }
    }

    private void WriteAssembledResponse(StreamEvent evt)
    {
        if (_finalized) return;
        _finalized = true;
        evt.IsComplete = true;

        var finishReason = ComputeFinishReason();

        var message = new JsonObject { ["role"] = "assistant" };
        message["content"] = _content.Length > 0 ? _content.ToString() : null;

        if (_toolCallsBuffer.Count > 0)
        {
            var toolCallsArray = new JsonArray();
            for (int i = 0; i < _toolCallsBuffer.Count; i++)
            {
                var (callId, name, arguments) = _toolCallsBuffer[i];
                toolCallsArray.Add(new JsonObject
                {
                    ["id"] = callId,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = name,
                        ["arguments"] = arguments.ToString()
                    }
                });
            }
            message["tool_calls"] = toolCallsArray;
        }

        var response = new JsonObject
        {
            ["id"] = _id,
            ["object"] = "chat.completion",
            ["created"] = _created,
            ["model"] = _model,
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["message"] = message,
                    ["finish_reason"] = finishReason
                }
            },
            ["usage"] = new JsonObject
            {
                ["prompt_tokens"] = _inputTokens,
                ["completion_tokens"] = _outputTokens,
                ["total_tokens"] = _inputTokens + _outputTokens,
                ["prompt_tokens_details"] = new JsonObject
                {
                    ["cached_tokens"] = _cachedTokens
                }
            }
        };

        evt.ConvertedBytes = Encoding.UTF8.GetBytes(response.ToJsonString());

        if (_inputTokens > 0 || _outputTokens > 0)
            evt.Usage = new ResponseUsage(_inputTokens, _outputTokens, _cachedTokens);
    }

    private string ComputeFinishReason()
    {
        if (_responseStatus == "incomplete")
            return _incompleteReason == "max_output_tokens" ? "length" : "stop";
        if (_sawToolCalls)
            return "tool_calls";
        return "stop";
    }

    private static string GenerateChatCmplId()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return "chatcmpl-" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

using System.Text;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi.Converter;

/// <summary>
/// Chat Completions → Responses API 格式转换器（请求体）
/// </summary>
public static class ChatCompletionsConverter
{
    public static bool IsChatCompletionsFormat(JsonObject body)
        => body.ContainsKey("messages") && !body.ContainsKey("input");

    public static JsonObject ConvertRequestBody(JsonObject chatReq)
    {
        var req = new JsonObject
        {
            ["model"] = chatReq["model"]?.DeepClone(),
            ["stream"] = true,
            ["store"] = false,
            ["include"] = new JsonArray { "reasoning.encrypted_content" }
        };

        // messages → input
        if (chatReq.TryGetPropertyValue("messages", out var messagesNode) && messagesNode is JsonArray messages)
            req["input"] = ConvertMessages(messages);

        // max_completion_tokens / max_tokens → max_output_tokens，最小值 128（防止极小值截断响应）
        const int minMaxOutputTokens = 128;
        if (chatReq.TryGetPropertyValue("max_completion_tokens", out var maxComp) && maxComp != null)
        {
            var v = maxComp.DeepClone();
            if (v is JsonValue jv && jv.TryGetValue<int>(out var iv) && iv < minMaxOutputTokens)
                v = JsonValue.Create(minMaxOutputTokens);
            req["max_output_tokens"] = v;
        }
        else if (chatReq.TryGetPropertyValue("max_tokens", out var maxTok) && maxTok != null)
        {
            var v = maxTok.DeepClone();
            if (v is JsonValue jv && jv.TryGetValue<int>(out var iv) && iv < minMaxOutputTokens)
                v = JsonValue.Create(minMaxOutputTokens);
            req["max_output_tokens"] = v;
        }

        // temperature, top_p
        if (chatReq.TryGetPropertyValue("temperature", out var temp) && temp != null)
            req["temperature"] = temp.DeepClone();
        if (chatReq.TryGetPropertyValue("top_p", out var topP) && topP != null)
            req["top_p"] = topP.DeepClone();

        // reasoning_effort → reasoning
        if (chatReq.TryGetPropertyValue("reasoning_effort", out var effort) && effort != null)
            req["reasoning"] = new JsonObject { ["effort"] = effort.DeepClone(), ["summary"] = "auto" };

        // tools（优先使用 tools，无 tools 时再看 functions）
        if (chatReq.TryGetPropertyValue("tools", out var tools) && tools is JsonArray toolsArray)
            req["tools"] = ConvertTools(toolsArray);
        else if (chatReq.TryGetPropertyValue("functions", out var functions) && functions is JsonArray funcArray && funcArray.Count > 0)
            req["tools"] = ConvertFunctionsToTools(funcArray);

        // tool_choice（优先使用 tool_choice，无则看 function_call）
        if (chatReq.TryGetPropertyValue("tool_choice", out var toolChoice) && toolChoice != null)
            req["tool_choice"] = toolChoice.DeepClone();
        else if (chatReq.TryGetPropertyValue("function_call", out var functionCall) && functionCall != null)
            req["tool_choice"] = ConvertFunctionCallToToolChoice(functionCall);

        // service_tier
        if (chatReq.TryGetPropertyValue("service_tier", out var tier) && tier != null)
            req["service_tier"] = tier.DeepClone();

        return req;
    }

    private static JsonArray ConvertMessages(JsonArray messages)
    {
        var input = new JsonArray();
        foreach (var msg in messages)
        {
            if (msg is not JsonObject msgObj) continue;
            var role = msgObj.TryGetPropertyValue("role", out var r) ? r?.GetValue<string>() : null;
            switch (role)
            {
                case "system":
                    input.Add(ConvertSystemMessage(msgObj));
                    break;
                case "user":
                    input.Add(ConvertUserMessage(msgObj));
                    break;
                case "assistant":
                    foreach (var item in ConvertAssistantMessage(msgObj))
                        input.Add(item);
                    break;
                case "tool":
                    input.Add(ConvertToolMessage(msgObj));
                    break;
                case "function":
                    input.Add(ConvertFunctionRoleMessage(msgObj));
                    break;
            }
        }
        return input;
    }

    private static JsonObject ConvertSystemMessage(JsonObject msg)
    {
        var contentNode = msg["content"];
        return new JsonObject
        {
            ["role"] = "system",
            ["content"] = MarshalContent(contentNode, isOutput: false)
        };
    }

    private static JsonObject ConvertUserMessage(JsonObject msg)
    {
        var contentNode = msg["content"];
        return new JsonObject
        {
            ["role"] = "user",
            ["content"] = MarshalContent(contentNode, isOutput: false)
        };
    }

    /// <summary>
    /// 字符串 content → 直接透传字符串；parts 数组 → 转换为 ResponsesContentPart 数组
    /// </summary>
    private static JsonNode MarshalContent(JsonNode? contentNode, bool isOutput)
    {
        if (contentNode is JsonValue strVal && strVal.TryGetValue<string>(out var str))
        {
            // 字符串直接透传（与 sub2api 一致）
            return JsonValue.Create(str)!;
        }

        if (contentNode is JsonArray parts)
        {
            var arr = new JsonArray();
            foreach (var part in parts)
            {
                if (part is not JsonObject partObj) continue;
                var type = partObj.TryGetPropertyValue("type", out var t) ? t?.GetValue<string>() : null;
                if (type == "text")
                {
                    arr.Add(new JsonObject
                    {
                        ["type"] = isOutput ? "output_text" : "input_text",
                        ["text"] = partObj["text"]?.DeepClone()
                    });
                }
                else if (type == "image_url")
                {
                    var imageUrl = partObj["image_url"];
                    string? url = null;
                    if (imageUrl is JsonObject imageUrlObj)
                        imageUrlObj.TryGetPropertyValue("url", out var u);
                    else if (imageUrl is JsonValue v)
                        url = v.GetValue<string>();

                    if (imageUrl is JsonObject iuObj && iuObj.TryGetPropertyValue("url", out var urlNode))
                        url = urlNode?.GetValue<string>();

                    if (url != null)
                        arr.Add(new JsonObject { ["type"] = "input_image", ["image_url"] = url });
                }
            }
            return arr;
        }

        return JsonValue.Create(string.Empty)!;
    }

    private static IEnumerable<JsonObject> ConvertAssistantMessage(JsonObject msg)
    {
        var contentNode = msg["content"];
        var toolCalls = msg.TryGetPropertyValue("tool_calls", out var tc) ? tc as JsonArray : null;

        // 文本内容
        string? textContent = null;
        if (contentNode is JsonValue strVal && strVal.TryGetValue<string>(out var str) && !string.IsNullOrEmpty(str))
        {
            textContent = str;
        }
        else if (contentNode is JsonArray parts)
        {
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part is JsonObject partObj &&
                    partObj.TryGetPropertyValue("type", out var t))
                {
                    var type = t?.GetValue<string>();
                    if ((type == "thinking" || type == "reasoning") &&
                        partObj.TryGetPropertyValue("thinking", out var thinkingNode) &&
                        thinkingNode?.GetValue<string>() is { Length: > 0 } thinkingText)
                    {
                        sb.Append("<thinking>").Append(thinkingText).Append("</thinking>");
                    }
                    else if (type == "text" &&
                        partObj.TryGetPropertyValue("text", out var txt))
                        sb.Append(txt?.GetValue<string>());
                }
            }
            if (sb.Length > 0) textContent = sb.ToString();
        }

        if (!string.IsNullOrEmpty(textContent))
        {
            // role-based message: 不带 type 字段
            yield return new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = new JsonArray
                {
                    new JsonObject { ["type"] = "output_text", ["text"] = textContent }
                }
            };
        }

        // tool_calls → function_call items（独立 item，不在 message 内）
        if (toolCalls != null)
        {
            foreach (var tc2 in toolCalls)
            {
                if (tc2 is not JsonObject tcObj) continue;
                var funcNode = tcObj.TryGetPropertyValue("function", out var fn) ? fn as JsonObject : null;
                if (funcNode == null) continue;

                var callId = tcObj.TryGetPropertyValue("id", out var id) ? id?.GetValue<string>() : null;
                var funcName = funcNode.TryGetPropertyValue("name", out var n) ? n?.GetValue<string>() : null;
                var funcArgs = funcNode.TryGetPropertyValue("arguments", out var a) ? a?.GetValue<string>() : null;
                if (string.IsNullOrEmpty(funcArgs)) funcArgs = "{}";

                yield return new JsonObject
                {
                    ["type"] = "function_call",
                    ["call_id"] = callId,
                    ["name"] = funcName,
                    ["arguments"] = funcArgs
                };
            }
        }
    }

    private static JsonObject ConvertToolMessage(JsonObject msg)
    {
        var toolCallId = msg.TryGetPropertyValue("tool_call_id", out var id) ? id?.GetValue<string>() : null;
        string? output = null;
        if (msg.TryGetPropertyValue("content", out var c))
        {
            if (c is JsonValue cv && cv.TryGetValue<string>(out var s))
                output = s;
            else if (c is JsonArray arr)
            {
                var sb = new StringBuilder();
                foreach (var part in arr)
                    if (part is JsonObject partObj &&
                        partObj.TryGetPropertyValue("type", out var t) && t?.GetValue<string>() == "text" &&
                        partObj.TryGetPropertyValue("text", out var txt))
                        sb.Append(txt?.GetValue<string>());
                output = sb.ToString();
            }
        }
        if (string.IsNullOrEmpty(output)) output = "(empty)";

        return new JsonObject
        {
            ["type"] = "function_call_output",
            ["call_id"] = toolCallId,
            ["output"] = output
        };
    }

    private static JsonArray ConvertTools(JsonArray tools)
    {
        var result = new JsonArray();
        foreach (var tool in tools)
        {
            if (tool is not JsonObject toolObj) continue;
            var type = toolObj.TryGetPropertyValue("type", out var t) ? t?.GetValue<string>() : null;
            if (type != "function") continue;

            var func = toolObj.TryGetPropertyValue("function", out var f) ? f as JsonObject : null;
            if (func == null) continue;

            var converted = new JsonObject { ["type"] = "function" };
            if (func.TryGetPropertyValue("name", out var n) && n != null) converted["name"] = n.DeepClone();
            if (func.TryGetPropertyValue("description", out var d) && d != null) converted["description"] = d.DeepClone();
            if (func.TryGetPropertyValue("parameters", out var p) && p != null) converted["parameters"] = p.DeepClone();
            if (func.TryGetPropertyValue("strict", out var s) && s != null) converted["strict"] = s.DeepClone();
            result.Add(converted);
        }
        return result;
    }

    /// <summary>
    /// Legacy role=function → function_call_output，用 message.name 作 call_id（旧版无独立 call_id）
    /// </summary>
    private static JsonObject ConvertFunctionRoleMessage(JsonObject msg)
    {
        var callId = msg.TryGetPropertyValue("name", out var n) ? n?.GetValue<string>() : null;
        string? output = null;
        if (msg.TryGetPropertyValue("content", out var c))
        {
            if (c is JsonValue cv && cv.TryGetValue<string>(out var s))
                output = s;
            else if (c is JsonArray arr)
            {
                var sb = new StringBuilder();
                foreach (var part in arr)
                    if (part is JsonObject partObj &&
                        partObj.TryGetPropertyValue("type", out var t) && t?.GetValue<string>() == "text" &&
                        partObj.TryGetPropertyValue("text", out var txt))
                        sb.Append(txt?.GetValue<string>());
                output = sb.ToString();
            }
        }
        if (string.IsNullOrEmpty(output)) output = "(empty)";

        return new JsonObject
        {
            ["type"] = "function_call_output",
            ["call_id"] = callId,
            ["output"] = output
        };
    }

    /// <summary>
    /// Legacy functions[] → Responses API tools[]
    /// functions[] 的每一项直接就是 {name, description, parameters, strict}，无嵌套 function 字段
    /// </summary>
    private static JsonArray ConvertFunctionsToTools(JsonArray functions)
    {
        var result = new JsonArray();
        foreach (var func in functions)
        {
            if (func is not JsonObject funcObj) continue;
            var converted = new JsonObject { ["type"] = "function" };
            if (funcObj.TryGetPropertyValue("name", out var n) && n != null) converted["name"] = n.DeepClone();
            if (funcObj.TryGetPropertyValue("description", out var d) && d != null) converted["description"] = d.DeepClone();
            if (funcObj.TryGetPropertyValue("parameters", out var p) && p != null) converted["parameters"] = p.DeepClone();
            if (funcObj.TryGetPropertyValue("strict", out var s) && s != null) converted["strict"] = s.DeepClone();
            result.Add(converted);
        }
        return result;
    }

    /// <summary>
    /// Legacy function_call → tool_choice
    /// "auto"/"none" → 原样透传；{"name":"X"} → {"type":"function","function":{"name":"X"}}
    /// </summary>
    private static JsonNode ConvertFunctionCallToToolChoice(JsonNode functionCall)
    {
        if (functionCall is JsonValue strVal && strVal.TryGetValue<string>(out var s))
            return JsonValue.Create(s)!;

        if (functionCall is JsonObject obj && obj.TryGetPropertyValue("name", out var name) && name != null)
        {
            return new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = name.DeepClone() }
            };
        }

        return functionCall.DeepClone();
    }
}

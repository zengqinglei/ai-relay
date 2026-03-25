using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;

/// <summary>
/// OpenAI OAuth (ChatGPT Codex API) 请求体处理器
/// Chat Completions → Responses API 协议转换
/// </summary>
public class OpenAiRequestBodyProcessor(ChatModelConnectionOptions options, OpenAiCodexInjector openAiCodexInjector) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (down.BodyJsonNode is not JsonObject || down.BodyJsonNode == null)
        {
            return Task.CompletedTask;
        }

        var clonedBody = down.CloneBodyJson() ?? [];
        // 写入 mapped model id
        if (!string.IsNullOrEmpty(up.MappedModelId) && up.MappedModelId != down.ModelId)
            clonedBody["model"] = up.MappedModelId;

        if (options.Platform == ProviderPlatform.OPENAI_OAUTH)
        {
            // reasoning.effort 修正 (minimal -> none)
            if (clonedBody.TryGetPropertyValue("reasoning", out var reasoningNode) &&
            reasoningNode is JsonObject reasoningObj &&
            reasoningObj.TryGetPropertyValue("effort", out var effortNode) &&
            effortNode != null &&
            effortNode.GetValue<string>() == "minimal")
            {
                reasoningObj["effort"] = "none";
            }

            // 移除 Codex API 不支持的参数
            var unsupportedParams = new[]
            {
                "max_output_tokens", "max_completion_tokens", "temperature",
                "top_p", "frequency_penalty", "presence_penalty"
            };
            foreach (var param in unsupportedParams)
                clonedBody.Remove(param);

            // 工具规范化（Chat Completions -> Responses API）
            NormalizeCodexTools(clonedBody);

            // Input 过滤
            bool needsToolContinuation = NeedsToolContinuation(clonedBody);
            FilterCodexInput(clonedBody, needsToolContinuation);

            // Instructions 注入（Codex 模式）
            openAiCodexInjector.InjectCodexInstructions(clonedBody, down.GetUserAgent());

            // OAuth 模式必需字段
            clonedBody["store"] = false;
            clonedBody["stream"] = true;
        }

        up.BodyJson = clonedBody;
        up.SessionId = down.SessionId;
        return Task.CompletedTask;
    }

    private static bool NeedsToolContinuation(JsonObject jsonNode)
    {
        if (jsonNode.TryGetPropertyValue("previous_response_id", out var prevId) &&
            prevId is JsonValue prevIdValue &&
            !string.IsNullOrWhiteSpace(prevIdValue.GetValue<string>()))
            return true;

        if (jsonNode.TryGetPropertyValue("tools", out var tools) &&
            tools is JsonArray toolsArray && toolsArray.Count > 0)
            return true;

        if (jsonNode.TryGetPropertyValue("tool_choice", out var toolChoice) && toolChoice != null)
            return true;

        if (jsonNode.TryGetPropertyValue("input", out var inputNode) && inputNode is JsonArray input)
        {
            foreach (var item in input)
            {
                if (item is JsonObject itemObj &&
                    itemObj.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue)
                {
                    var type = typeValue.GetValue<string>();
                    if (type == "function_call_output" || type == "item_reference")
                        return true;
                }
            }
        }

        return false;
    }

    private static void FilterCodexInput(JsonObject jsonNode, bool preserveReferences)
    {
        if (!jsonNode.TryGetPropertyValue("input", out var inputNode) || inputNode is not JsonArray input)
            return;

        for (int i = input.Count - 1; i >= 0; i--)
        {
            if (input[i] is not JsonObject item) continue;

            var type = item.TryGetPropertyValue("type", out var t) ? t?.GetValue<string>() : null;

            if (type == "item_reference")
            {
                if (!preserveReferences)
                    input.RemoveAt(i);
                continue;
            }

            if (IsCodexToolCallItemType(type) &&
                (!item.TryGetPropertyValue("call_id", out var callId) ||
                 string.IsNullOrWhiteSpace(callId?.GetValue<string>())) &&
                item.TryGetPropertyValue("id", out var id) &&
                !string.IsNullOrWhiteSpace(id?.GetValue<string>()))
            {
                item["call_id"] = id.DeepClone();
            }

            if (!preserveReferences)
            {
                item.Remove("id");
                if (!IsCodexToolCallItemType(type))
                    item.Remove("call_id");
            }
        }
    }

    private static bool IsCodexToolCallItemType(string? type) =>
        type != null && (type.EndsWith("_call") || type.EndsWith("_call_output"));

    private static void NormalizeCodexTools(JsonObject jsonNode)
    {
        if (!jsonNode.TryGetPropertyValue("tools", out var toolsNode) || toolsNode is not JsonArray tools)
            return;

        for (int i = tools.Count - 1; i >= 0; i--)
        {
            if (tools[i] is not JsonObject tool) continue;

            var toolType = tool.TryGetPropertyValue("type", out var t) ? t?.GetValue<string>() : null;
            if (toolType != "function") continue;

            if (tool.TryGetPropertyValue("name", out var nameNode) &&
                nameNode != null && !string.IsNullOrWhiteSpace(nameNode.GetValue<string>()))
                continue;

            if (!tool.TryGetPropertyValue("function", out var funcNode) || funcNode is not JsonObject func)
            {
                tools.RemoveAt(i);
                continue;
            }

            if (func.TryGetPropertyValue("name", out var n) && n != null)
                tool["name"] = n.DeepClone();
            if (func.TryGetPropertyValue("description", out var d) && d != null)
                tool["description"] = d.DeepClone();
            if (func.TryGetPropertyValue("parameters", out var p) && p != null)
                tool["parameters"] = p.DeepClone();
            if (func.TryGetPropertyValue("strict", out var s) && s != null)
                tool["strict"] = s.DeepClone();
        }
    }
}

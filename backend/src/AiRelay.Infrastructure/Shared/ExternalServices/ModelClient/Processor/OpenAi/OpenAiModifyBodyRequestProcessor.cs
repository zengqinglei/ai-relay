using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi.Converter;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi;

/// <summary>
/// OpenAI OAuth (ChatGPT Codex API) 请求体处理器
/// Chat Completions → Responses API 协议转换
/// </summary>
public class OpenAiModifyBodyRequestProcessor(ChatModelConnectionOptions options, OpenAiCodexInjector openAiCodexInjector) : IRequestProcessor
{

    public async Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.SessionId = down.SessionId;

        // 必须检查 down.RelativePath（下游原始路径），而非 up.RelativePath
        // 因为 OpenAiUrlRequestProcessor 已将 up.RelativePath 统一改写为 /v1/responses
        bool isChatRoute = down.RelativePath.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase);
        bool isOAuth = options.AuthMethod == AuthMethod.OAuth;
        bool needChangeModel = !string.IsNullOrEmpty(up.MappedModelId) && up.MappedModelId != down.ModelId;

        // 如果既不是聊天生成接口，又不是 OAuth 需要调整参数，且无需修改模型，则直接走零分配转发，无需解析 JSON
        if (!isChatRoute && !isOAuth && !needChangeModel)
        {
            return;
        }

        // 零分配捷径：如果是聊天路由，非 OAuth，无改模型需求，且格式已经是目标格式 (含 input，无 messages)，则无需解析 JSON
        bool hasMessages = down.ExtractedProps.ContainsKey("has_openai_messages");
        bool hasInput = down.ExtractedProps.ContainsKey("has_openai_input");
        if (isChatRoute && !isOAuth && !needChangeModel && !hasMessages && hasInput)
        {
            return;
        }

        var clonedBody = await up.EnsureMutableBodyAsync(down);

        // 格式转换：Chat Completions → Responses API
        if (ChatCompletionsConverter.IsChatCompletionsFormat(clonedBody))
        {
            clonedBody = ChatCompletionsConverter.ConvertRequestBody(clonedBody);
            up.BodyJson = clonedBody; // 转换后必须回写，ConvertRequestBody 返回全新对象
        }

        // 写入 mapped model id
        if (!string.IsNullOrEmpty(up.MappedModelId) && up.MappedModelId != down.ModelId)
            clonedBody["model"] = up.MappedModelId;

        if (options.AuthMethod == AuthMethod.OAuth)
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

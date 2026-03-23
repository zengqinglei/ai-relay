using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Gemini;

/// <summary>
/// Gemini OAuth 请求体处理器：v1internal 包装、Schema 清洗、CLI 伪装元数据注入
/// </summary>
public class GeminiOAuthRequestBodyProcessor(
    ChatModelConnectionOptions options,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector) : IRequestProcessor
{
    // Gemini CLI user_prompt_id 格式: UUID + "########" + 数字
    private static readonly Regex UserPromptIdPattern = new(
        @"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}#{8}\d+$",
        RegexOptions.Compiled);

    // Gemini CLI User-Agent 正则
    private static readonly Regex GeminiCliUAPattern = new(@"^GeminiCLI/\d+\.\d+\.\d+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (down.BodyJsonNode is not JsonObject)
        {
            up.BodyJson = null;
            up.SessionId = down.SessionHash;
            return Task.CompletedTask;
        }

        var clonedBody = down.CloneBodyJson() ?? new JsonObject();
        bool wrapped = clonedBody.ContainsKey("request") && clonedBody.ContainsKey("project");

        // 确定内层 payload（用于 Schema 清洗和 CLI 元数据注入）
        var payload = wrapped
            ? clonedBody["request"] as JsonObject
            : clonedBody;

        // 清洗 JSON Schema（从内层 payload 取 tools）
        if (payload?["tools"] is JsonArray tools)
        {
            foreach (var tool in tools)
            {
                if (tool is not JsonObject toolObj) continue;

                var funcs = toolObj["function_declarations"]?.AsArray()
                         ?? toolObj["functionDeclarations"]?.AsArray();

                if (funcs != null)
                {
                    foreach (var func in funcs)
                    {
                        if (func is JsonObject funcObj && funcObj["parameters"] is JsonObject paramsObj)
                            googleJsonSchemaCleaner.Clean(paramsObj);
                    }
                }
            }
        }

        // CLI 伪装：未检测到真实 CLI 客户端时注入系统提示和元数据
        bool shouldMimic = options.ShouldMimicOfficialClient;
        bool isGeminiCli = shouldMimic && IsGeminiCliClient(down, clonedBody);

        JsonObject finalBody;

        var projectId = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
        if (wrapped)
        {
            // 已包装：不重新包装，只更新 project 字段
            if (!string.IsNullOrEmpty(projectId))
                clonedBody["project"] = projectId;

            if (shouldMimic && !isGeminiCli)
            {
                geminiSystemPromptInjector.InjectGeminiCliPrompt(clonedBody);
                // user_prompt_id 注入到顶层 wrapper，session_id 注入到内层 payload
                InjectGeminiCliMetadata(clonedBody, payload);
            }

            finalBody = clonedBody;
        }
        else
        {
            // 未包装：构建 v1internal 包装
            var modelId = up.MappedModelId ?? down.ModelId ?? "gemini-3.0-flash-preview";

            clonedBody.Remove("model");

            var wrapper = new JsonObject
            {
                ["model"] = modelId,
                ["project"] = projectId,
                ["request"] = clonedBody
            };

            if (shouldMimic && !isGeminiCli)
            {
                geminiSystemPromptInjector.InjectGeminiCliPrompt(wrapper);
                // user_prompt_id 注入到顶层 wrapper，session_id 注入到内层 clonedBody
                InjectGeminiCliMetadata(wrapper, clonedBody);
            }

            finalBody = wrapper;
        }

        up.BodyJson = finalBody;
        up.SessionId = down.SessionHash;
        return Task.CompletedTask;
    }

    private static bool IsGeminiCliClient(DownRequestContext down, JsonObject bodyJson)
    {
        var userAgent = down.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !GeminiCliUAPattern.IsMatch(userAgent))
            return false;

        // 获取内层 payload
        var payload = bodyJson.ContainsKey("request")
            ? bodyJson["request"] as JsonObject
            : bodyJson;

        if (payload == null) return false;

        // 验证 user_prompt_id 格式
        if (!payload.TryGetPropertyValue("user_prompt_id", out var promptIdNode) ||
            promptIdNode is not JsonValue promptIdValue ||
            !promptIdValue.TryGetValue<string>(out var userPromptId) ||
            string.IsNullOrEmpty(userPromptId) ||
            !UserPromptIdPattern.IsMatch(userPromptId))
            return false;

        // 验证 session_id 存在
        if (!payload.TryGetPropertyValue("session_id", out var sessionIdNode) ||
            sessionIdNode is not JsonValue sessionIdValue ||
            !sessionIdValue.TryGetValue<string>(out var sessionId) ||
            string.IsNullOrEmpty(sessionId))
            return false;

        // 验证 systemInstruction 包含 Gemini CLI 特征
        if (!payload.TryGetPropertyValue("systemInstruction", out var systemNode) ||
            systemNode is not JsonObject systemObj)
            return false;

        if (systemObj.TryGetPropertyValue("parts", out var partsNode) && partsNode is JsonArray parts)
        {
            foreach (var part in parts)
            {
                if (part is JsonObject partObj &&
                    partObj.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text) &&
                    text.Contains("Gemini CLI", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <param name="wrapper">顶层包装对象（user_prompt_id 注入此处）</param>
    /// <param name="payload">内层请求对象（session_id 注入此处）</param>
    private static void InjectGeminiCliMetadata(JsonObject wrapper, JsonObject? payload)
    {
        // 注入 session_id 到内层 payload
        if (payload != null && !payload.ContainsKey("session_id"))
            payload["session_id"] = Guid.NewGuid().ToString("D");

        // 注入 user_prompt_id 到顶层 wrapper: UUID + "########" + 随机数字
        if (!wrapper.ContainsKey("user_prompt_id"))
        {
            var randomDigit = Random.Shared.Next(0, 10);
            wrapper["user_prompt_id"] = $"{Guid.NewGuid():D}########{randomDigit}";
        }
    }
}

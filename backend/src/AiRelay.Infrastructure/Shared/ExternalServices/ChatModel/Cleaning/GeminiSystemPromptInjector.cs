using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;

/// <summary>
/// Gemini System Prompt 注入器
/// 负责为 Gemini OAuth 账号智能注入 Gemini CLI 系统提示词
/// </summary>
public class GeminiSystemPromptInjector
{
    public const string GeminiCliSystemPrompt = "You are Gemini CLI, an interactive CLI agent specializing in software engineering tasks.";

    /// <summary>
    /// 在 systemInstruction 开头注入 Gemini CLI 提示词（仅 OAuth 账号需要）
    /// </summary>
    public bool InjectGeminiCliPrompt(JsonObject requestJson)
    {
        try
        {
            // 获取内层 payload（已封装结构）
            var payload = requestJson.ContainsKey("request")
                ? requestJson["request"] as JsonObject
                : requestJson;

            if (payload == null) return false;

            // 检查 systemInstruction 中是否已有 Gemini CLI 提示词
            if (payload.TryGetPropertyValue("systemInstruction", out var systemNode))
            {
                if (SystemIncludesGeminiCliPrompt(systemNode))
                {
                    return false; // 已存在，不重复注入
                }
            }

            // 构建或更新 systemInstruction
            if (systemNode == null)
            {
                // 不存在：创建新的 systemInstruction
                payload["systemInstruction"] = new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["text"] = GeminiCliSystemPrompt
                        }
                    }
                };
            }
            else if (systemNode is JsonObject systemObj)
            {
                // 存在：在 parts[0].text 开头注入
                if (systemObj.TryGetPropertyValue("parts", out var partsNode) &&
                    partsNode is JsonArray parts &&
                    parts.Count > 0 &&
                    parts[0] is JsonObject firstPart &&
                    firstPart.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text))
                {
                    firstPart["text"] = $"{GeminiCliSystemPrompt}\n\n{text}";
                }
                else
                {
                    // parts 不存在或为空，创建新的
                    systemObj["parts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["text"] = GeminiCliSystemPrompt
                        }
                    };
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查 systemInstruction 中是否已包含 Gemini CLI 提示词
    /// </summary>
    private static bool SystemIncludesGeminiCliPrompt(JsonNode? systemNode)
    {
        if (systemNode is not JsonObject systemObj) return false;

        if (systemObj.TryGetPropertyValue("parts", out var partsNode) &&
            partsNode is JsonArray parts)
        {
            foreach (var part in parts)
            {
                if (part is JsonObject partObj &&
                    partObj.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text) &&
                    text.Contains(GeminiCliSystemPrompt, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

using System.Text.Json.Nodes;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Gemini;

/// <summary>
/// Gemini API Key 请求体处理器：JSON Schema 清洗、CLI 系统提示注入
/// </summary>
public class GeminiApiKeyRequestBodyProcessor(
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector,
    bool shouldMimic) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (down.BodyJsonNode is not JsonObject || down.BodyJsonNode == null)
        {
            return Task.CompletedTask;
        }

        var clonedBody = down.CloneBodyJson() ?? [];
        // 聊天接口特有处理
        if (up.RelativePath.EndsWith(":streamGenerateContent") || up.RelativePath.EndsWith(":generateContent"))
        {
            // 清洗 JSON Schema
            if (clonedBody["tools"] is JsonArray tools)
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

            // 伪装逻辑：未检测到真实 CLI 客户端时注入系统提示
            if (shouldMimic && !IsGeminiCliClient(down, clonedBody))
                geminiSystemPromptInjector.InjectGeminiCliPrompt(clonedBody);
        }

        up.BodyJson = clonedBody;
        up.SessionId = down.SessionHash;
        return Task.CompletedTask;
    }

    private static bool IsGeminiCliClient(DownRequestContext down, JsonObject? requestJson)
    {
        var userAgent = down.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !userAgent.StartsWith("GeminiCLI/", StringComparison.OrdinalIgnoreCase))
            return false;

        return !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-goog-api-client")) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-gemini-api-privileged-user-id")) &&
               requestJson != null &&
               HasGeminiCliSystemPrompt(requestJson);
    }

    private static bool HasGeminiCliSystemPrompt(JsonObject requestJson)
    {
        if (!requestJson.TryGetPropertyValue("systemInstruction", out var systemNode) ||
            systemNode is not JsonObject systemObj ||
            !systemObj.TryGetPropertyValue("parts", out var partsNode) ||
            partsNode is not JsonArray parts)
            return false;

        foreach (var part in parts)
        {
            if (part is JsonObject partObj &&
                partObj.TryGetPropertyValue("text", out var textNode) &&
                textNode is JsonValue textValue &&
                textValue.TryGetValue<string>(out var text) &&
                text.Contains("Gemini CLI", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

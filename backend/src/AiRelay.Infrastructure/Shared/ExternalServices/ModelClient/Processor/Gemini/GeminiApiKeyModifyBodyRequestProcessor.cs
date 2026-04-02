using System.Text.Json.Nodes;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;

/// <summary>
/// Gemini API Key 请求体处理器：JSON Schema 清洗、CLI 系统提示注入
/// </summary>
public class GeminiApiKeyModifyBodyRequestProcessor(
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector,
    bool shouldMimic) : IRequestProcessor
{

    public async Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.SessionId = down.SessionId;
        
        // 聊天接口特有处理
        bool isChatRoute = up.RelativePath.EndsWith(":streamGenerateContent", StringComparison.OrdinalIgnoreCase) || 
                           up.RelativePath.EndsWith(":generateContent", StringComparison.OrdinalIgnoreCase);

        if (!isChatRoute)
        {
            return;
        }

        // 零分配捷径：如果无 tools 参数且（不需要伪装或本身就是 CLI），直接跳过解析
        bool hasTools = down.ExtractedProps.ContainsKey("has_tools");
        bool isGeminiCli = IsGeminiCliClient(down);
        if (!hasTools && (!shouldMimic || isGeminiCli))
        {
            return;
        }

        var clonedBody = await up.EnsureMutableBodyAsync(down);

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
        if (shouldMimic && !isGeminiCli)
            geminiSystemPromptInjector.InjectGeminiCliPrompt(clonedBody);
    }

    private static bool IsGeminiCliClient(DownRequestContext down)
    {
        var userAgent = down.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !userAgent.StartsWith("GeminiCLI/", StringComparison.OrdinalIgnoreCase))
            return false;

        return !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-goog-api-client")) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-gemini-api-privileged-user-id")) &&
               down.ExtractedProps.ContainsKey("is_gemini_cli_prompt");
    }
}

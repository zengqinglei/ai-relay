using System.Text.Json.Nodes;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Cleaning;

/// <summary>
/// Gemini Tools 清洗器
/// 负责修复 Gemini CLI 工具格式（parametersJsonSchema → parameters）
/// </summary>
public class GeminiToolsCleaner
{
    /// <summary>
    /// 修复 Gemini CLI tools 格式
    /// 将 functionDeclarations[].parametersJsonSchema 重命名为 parameters
    /// </summary>
    public void FixGeminiCliTools(JsonObject requestJson)
    {
        if (requestJson["tools"] is not JsonArray tools) return;

        foreach (var tool in tools)
        {
            if (tool is not JsonObject toolObj) continue;

            // 兼容 camelCase 和 snake_case
            JsonArray? funcs = null;
            if (toolObj["functionDeclarations"] is JsonArray fd) funcs = fd;
            else if (toolObj["function_declarations"] is JsonArray fdSnake) funcs = fdSnake;

            if (funcs == null) continue;

            foreach (var func in funcs)
            {
                if (func is not JsonObject funcObj) continue;

                // 修复：parametersJsonSchema → parameters
                if (funcObj.ContainsKey("parametersJsonSchema"))
                {
                    var schema = funcObj["parametersJsonSchema"];
                    funcObj.Remove("parametersJsonSchema");
                    if (!funcObj.ContainsKey("parameters"))
                    {
                        funcObj["parameters"] = schema;
                    }
                }
            }
        }
    }
}

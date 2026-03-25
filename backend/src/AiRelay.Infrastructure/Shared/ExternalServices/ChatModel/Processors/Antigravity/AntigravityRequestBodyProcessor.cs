using System.Text.Json.Nodes;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Antigravity;

/// <summary>
/// Antigravity 请求体处理器：v1internal 包装、身份注入、Schema 清洗、签名注入
/// </summary>
public class AntigravityRequestBodyProcessor(
    ChatModelConnectionOptions options,
    AntigravityIdentityInjector antigravityIdentityInjector,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    ILogger logger) : IRequestProcessor
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
            // 协议必需：注入 Antigravity 特有字段
            antigravityIdentityInjector.EnsureAntigravityIdentity(clonedBody);
            FixGeminiCliTools(clonedBody);

            // 清洗 JSON Schema
            if (clonedBody["tools"] is JsonArray tools)
            {
                foreach (var tool in tools)
                {
                    if (tool is not JsonObject toolObj) continue;
                    var func = toolObj["functionDeclarations"]?.AsArray().FirstOrDefault()
                               ?? toolObj["function"];
                    if (func is JsonObject funcObj && funcObj["parameters"] is JsonObject paramsObj)
                    {
                        googleJsonSchemaCleaner.Clean(paramsObj);
                    }
                }
            }
        }

        // 构建 v1internal 包装
        clonedBody.Remove("model");
        var requestType = DetermineRequestType(up.MappedModelId ?? string.Empty, clonedBody);
        var projectId = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";

        var wrapper = new JsonObject
        {
            ["project"] = projectId,
            ["requestId"] = $"agent-{down.StickySessionId ?? Guid.NewGuid().ToString("D")}",
            ["userAgent"] = "antigravity",
            ["requestType"] = requestType,
            ["model"] = up.MappedModelId,
            ["request"] = clonedBody
        };

        logger.LogDebug("已构建 Antigravity 请求: Model={Model}, Type={Type}", up.MappedModelId, requestType);

        up.BodyJson = wrapper;
        up.SessionId = down.SessionHash;
        return Task.CompletedTask;
    }

    private static void FixGeminiCliTools(JsonObject requestJson)
    {
        if (requestJson["tools"] is not JsonArray tools) return;
        foreach (var tool in tools)
        {
            if (tool is not JsonObject toolObj) continue;
            JsonArray? funcs = toolObj["functionDeclarations"] as JsonArray
                            ?? toolObj["function_declarations"] as JsonArray;
            if (funcs == null) continue;
            foreach (var func in funcs)
            {
                if (func is not JsonObject funcObj) continue;
                if (funcObj.ContainsKey("parametersJsonSchema"))
                {
                    var schema = funcObj["parametersJsonSchema"];
                    funcObj.Remove("parametersJsonSchema");
                    if (!funcObj.ContainsKey("parameters"))
                        funcObj["parameters"] = schema;
                }
            }
        }
    }

    private static string DetermineRequestType(string modelId, JsonObject requestJson)
    {
        if (modelId.Contains("image", StringComparison.OrdinalIgnoreCase)) return "image_gen";

        bool hasOnlineSuffix = modelId.EndsWith("-online", StringComparison.OrdinalIgnoreCase);
        bool hasNetworkingTool = false;

        if (requestJson.TryGetPropertyValue("tools", out var toolsNode) && toolsNode is JsonArray toolsArray)
        {
            foreach (var tool in toolsArray)
            {
                if (tool is JsonObject toolObj &&
                   (toolObj.ContainsKey("googleSearch") || toolObj.ContainsKey("google_search_retrieval")))
                {
                    hasNetworkingTool = true;
                    break;
                }
            }
        }

        if (hasOnlineSuffix || hasNetworkingTool) return "web_search";
        return "agent";
    }
}

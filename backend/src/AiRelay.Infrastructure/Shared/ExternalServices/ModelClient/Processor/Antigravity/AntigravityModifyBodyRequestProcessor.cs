using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Antigravity;

/// <summary>
/// Antigravity 请求体处理器：v1internal 包装、身份注入、Schema 清洗、签名注入
/// </summary>
public class AntigravityModifyBodyRequestProcessor(
    ChatModelConnectionOptions options,
    AntigravityIdentityInjector antigravityIdentityInjector,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    ILogger logger) : IRequestProcessor
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

        var clonedBody = await up.EnsureMutableBodyAsync(down);

        GeminiContentPartsCleaner.FilterEmptyParts(clonedBody);
        GeminiContentPartsCleaner.EnsureFunctionCallThoughtSignatures(clonedBody, null);

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

        // 构建 v1internal 包装
        clonedBody.Remove("model");
        var requestType = DetermineRequestType(up.MappedModelId ?? string.Empty, clonedBody);
        var projectId = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";

        clonedBody = new JsonObject
        {
            ["project"] = projectId,
            ["requestId"] = $"agent-{down.StickySessionId ?? Guid.NewGuid().ToString("D")}",
            ["userAgent"] = "antigravity",
            ["requestType"] = requestType,
            ["model"] = up.MappedModelId,
            ["request"] = clonedBody
        };
        up.BodyJson = clonedBody; // 包装后必须回写，否则 BuildHttpRequestMessage 序列化的仍是原始未包装 body

        logger.LogDebug("已构建 Antigravity 请求: Model={Model}, Type={Type}", up.MappedModelId, requestType);
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

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;

/// <summary>
/// Gemini OAuth 请求体处理器：v1internal 包装、Schema 清洗、CLI 伪装元数据注入
/// </summary>
public class GeminiOAuthModifyBodyRequestProcessor(
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

        bool hasTools = down.ExtractedProps.ContainsKey("has_tools");
        bool isGeminiCli = IsGeminiCliClient(down);
        bool shouldMimic = options.ShouldMimicOfficialClient;
        bool isPackaged = down.ExtractedProps.ContainsKey("has_v1internal_request") && down.ExtractedProps.ContainsKey("has_v1internal_project");

        // 零分配捷径：如果已经包装过了 (v1internal结构) 且没有 tools 参数且不需要额外包装欺诈，则跳过解析
        if (isPackaged && !hasTools && (!shouldMimic || isGeminiCli))
        {
            return;
        }

        var clonedBody = await up.EnsureMutableBodyAsync(down);

        var projectId = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
        if (!clonedBody.ContainsKey("request") || !clonedBody.ContainsKey("project"))
        {
            // 未包装：构建 v1internal 包装
            var modelId = up.MappedModelId ?? down.ModelId ?? "gemini-3.0-flash-preview";
            clonedBody.Remove("model");
            clonedBody = new JsonObject
            {
                ["model"] = modelId,
                ["project"] = projectId,
                ["request"] = clonedBody
            };
            // ⭐ 关键：包装后必须回写到 up.BodyJson，否则 BuildHttpRequestMessage 序列化的仍是原始未包装 body
            up.BodyJson = clonedBody;
        }
        // 确定内层 payload（用于 Schema 清洗和 CLI 元数据注入）
        var payload = clonedBody["request"] as JsonObject;
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
        if (shouldMimic && !isGeminiCli)
        {
            geminiSystemPromptInjector.InjectGeminiCliPrompt(payload);
            // user_prompt_id 注入到顶层 wrapper，session_id 注入到内层 clonedBody
            InjectGeminiCliMetadata(clonedBody, payload, options, down);
        }
    }

    private static bool IsGeminiCliClient(DownRequestContext down)
    {
        var userAgent = down.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !GeminiCliUAPattern.IsMatch(userAgent))
            return false;

        if (!down.ExtractedProps.ContainsKey("is_gemini_cli_prompt"))
            return false;

        // 验证 user_prompt_id 格式（顶层属性，由 ExtractEssentialProps 自动提取）
        if (!down.ExtractedProps.TryGetValue("user_prompt_id", out var userPromptId) ||
            string.IsNullOrEmpty(userPromptId) ||
            !UserPromptIdPattern.IsMatch(userPromptId))
            return false;

        // 验证 session_id 存在（优先从 request.session_id，其次顶层 session_id）
        var hasSessionId = (down.ExtractedProps.TryGetValue("request.session_id", out var sid1) && !string.IsNullOrEmpty(sid1))
                        || (down.ExtractedProps.TryGetValue("session_id", out var sid2) && !string.IsNullOrEmpty(sid2));

        return hasSessionId;
    }

    /// <param name="wrapper">顶层包装对象（user_prompt_id 注入此处）</param>
    /// <param name="payload">内层请求对象（session_id 注入此处）</param>
    private static void InjectGeminiCliMetadata(JsonObject wrapper, JsonObject? payload, ChatModelConnectionOptions options, DownRequestContext down)
    {
        var sessionId = down.StickySessionId ?? Guid.NewGuid().ToString("D");

        // 注入 session_id 到内层 payload
        if (payload != null && !payload.ContainsKey("session_id"))
            payload["session_id"] = sessionId;

        // 注入 user_prompt_id 到顶层 wrapper: UUID + "########" + 递增数字
        if (!wrapper.ContainsKey("user_prompt_id"))
        {
            wrapper["user_prompt_id"] = $"{sessionId}########{down.PromptIndex}";
        }
    }
}

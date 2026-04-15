using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;

/// <summary>
/// Google 系平台统一请求体处理器
/// 覆盖：Antigravity（OAuth）、Gemini OAuth（Code Assist）、Gemini ApiKey（AI Studio）
///
/// 职责（按平台分支）：
///   Antigravity   → v1internal 包装（含 project/requestId/userAgent/requestType）、身份注入、Schema 清洗
///   Gemini OAuth  → v1internal 包装（含 project/model）、Schema 清洗、CLI 伪装元数据注入
///   Gemini ApiKey → Schema 清洗、CLI 系统提示注入（无 v1internal 包装）
///
/// 公共逻辑（所有平台）：
///   - isChatRoute 早退（非聊天路由直接透传）
///   - GeminiContentPartsCleaner（过滤空 parts、补全签名）
///   - JSON Schema 清洗（tools/functionDeclarations）
/// </summary>
public partial class GoogleModifyBodyRequestProcessor(
    ChatModelConnectionOptions options,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    ILogger logger,
    AntigravityIdentityInjector? antigravityIdentityInjector = null,
    GeminiSystemPromptInjector? geminiSystemPromptInjector   = null,
    ISignatureCache? signatureCache                           = null) : IRequestProcessor
{
    // Gemini CLI user_prompt_id 格式: UUID + "########" + 数字
    [GeneratedRegex(@"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}#{8}\d+$")]
    private static partial Regex UserPromptIdRegex();

    // Gemini CLI User-Agent 格式: GeminiCLI/x.y.z
    [GeneratedRegex(@"^GeminiCLI/\d+\.\d+\.\d+", RegexOptions.IgnoreCase)]
    private static partial Regex GeminiCliUARegex();

    public async Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.SessionId = down.SessionId;

        // 公共早退：仅聊天接口需要处理 Body
        bool isChatRoute = up.RelativePath.EndsWith(":streamGenerateContent", StringComparison.OrdinalIgnoreCase) ||
                           up.RelativePath.EndsWith(":generateContent", StringComparison.OrdinalIgnoreCase);
        if (!isChatRoute) return;

        if (options.Provider == Provider.Antigravity)
            await ProcessAntigravityBodyAsync(down, up);
        else if (options.AuthMethod == AuthMethod.OAuth)
            await ProcessGeminiOAuthBodyAsync(down, up);
        else
            await ProcessGeminiApiKeyBodyAsync(down, up);
    }

    // ── Antigravity ───────────────────────────────────────────────────────────

    private async Task ProcessAntigravityBodyAsync(DownRequestContext down, UpRequestContext up)
    {
        // 零分配早退：若身份已注入 且 无 tools 清洗需求 且 已是 v1internal 包装，则跳过全部克隆
        bool identityAlreadyInjected = down.ExtractedProps.ContainsKey("google.has_identity");
        bool hasTools                = down.ExtractedProps.ContainsKey("public.has_tools");
        bool isAlreadyPackaged       = down.ExtractedProps.ContainsKey("google.has_v1internal") &&
                                       down.ExtractedProps.ContainsKey("google.has_project");
        if (identityAlreadyInjected && !hasTools && isAlreadyPackaged) return;

        var body = await up.EnsureMutableBodyAsync(down);

        // 公共清理
        GeminiContentPartsCleaner.FilterEmptyParts(body);
        GeminiContentPartsCleaner.EnsureFunctionCallThoughtSignatures(body, null);

        // Antigravity 协议必需：身份注入
        antigravityIdentityInjector?.EnsureAntigravityIdentity(body);

        // 修复 Gemini CLI 工具格式（parametersJsonSchema → parameters）
        FixGeminiCliTools(body);

        // JSON Schema 清洗
        CleanToolSchemas(body);

        // 构建 v1internal 包装
        body.Remove("model");
        var requestType = DetermineRequestType(up.MappedModelId ?? string.Empty, body);
        var projectId   = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";

        up.BodyJson = new JsonObject
        {
            ["project"]     = projectId,
            ["requestId"]   = $"agent-{down.StickySessionId ?? Guid.NewGuid().ToString("D")}",
            ["userAgent"]   = "antigravity",
            ["requestType"] = requestType,
            ["model"]       = up.MappedModelId,
            ["request"]     = body
        };

        logger.LogDebug("已构建 Antigravity 请求: Model={Model}, Type={Type}", up.MappedModelId, requestType);
    }

    // ── Gemini OAuth（Code Assist）────────────────────────────────────────────

    private async Task ProcessGeminiOAuthBodyAsync(DownRequestContext down, UpRequestContext up)
    {
        bool hasTools    = down.ExtractedProps.ContainsKey("public.has_tools");
        bool isGeminiCli = IsGeminiOAuthCliClient(down);
        bool shouldMimic = options.ShouldMimicOfficialClient;
        bool isPackaged  = down.ExtractedProps.ContainsKey("google.has_v1internal") &&
                           down.ExtractedProps.ContainsKey("google.has_project");

        // 零分配捷径：已包装且无需额外修改时，仅做 Body 清理
        bool canSkipMutation = isPackaged && !hasTools && (!shouldMimic || isGeminiCli);

        var body = await up.EnsureMutableBodyAsync(down);

        bool isV1Internal = up.RelativePath.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase);

        // 未包装时构建 v1internal wrapper
        if (isV1Internal && !body.ContainsKey("request") && !body.ContainsKey("project"))
        {
            var projectId = options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
            var modelId   = up.MappedModelId ?? down.ModelId ?? "gemini-2.5-flash";
            body.Remove("model");
            body = new JsonObject
            {
                ["model"]   = modelId,
                ["project"] = projectId,
                ["request"] = body
            };
            up.BodyJson = body;
        }

        // 确定内层 payload（v1internal 取 request 字段，AI Studio 取自身）
        var payload = isV1Internal ? body["request"] as JsonObject : body;

        // 公共清理（始终执行，确保已包装请求的内层 payload 也能修复）
        if (payload != null)
        {
            GeminiContentPartsCleaner.FilterEmptyParts(payload);
            var cachedSig = !string.IsNullOrEmpty(up.SessionId) ? signatureCache?.GetSignature(up.SessionId) : null;
            GeminiContentPartsCleaner.EnsureFunctionCallThoughtSignatures(payload, cachedSig);
        }

        if (canSkipMutation) return;

        // JSON Schema 清洗（从内层 payload 取 tools）
        if (payload != null) CleanToolSchemas(payload);

        // CLI 伪装
        if (shouldMimic && !isGeminiCli)
        {
            geminiSystemPromptInjector?.InjectGeminiCliPrompt(payload);
            if (isV1Internal)
                InjectGeminiOAuthCliMetadata(body, payload, down);
        }
    }

    // ── Gemini ApiKey（AI Studio）─────────────────────────────────────────────

    private async Task ProcessGeminiApiKeyBodyAsync(DownRequestContext down, UpRequestContext up)
    {
        bool hasTools    = down.ExtractedProps.ContainsKey("public.has_tools");
        bool isGeminiCli = IsGeminiApiKeyCliClient(down);
        bool shouldMimic = options.ShouldMimicOfficialClient;

        // 零分配捷径：无 Schema 清洗且无需伪装
        bool canSkipMutation = !hasTools && (!shouldMimic || isGeminiCli);

        var body = await up.EnsureMutableBodyAsync(down);

        // 公共清理
        GeminiContentPartsCleaner.FilterEmptyParts(body);
        GeminiContentPartsCleaner.EnsureFunctionCallThoughtSignatures(body, null);

        if (canSkipMutation) return;

        // JSON Schema 清洗
        CleanToolSchemas(body);

        // CLI 伪装：注入系统提示
        if (shouldMimic && !isGeminiCli)
            geminiSystemPromptInjector?.InjectGeminiCliPrompt(body);
    }

    // ── 公共工具方法 ──────────────────────────────────────────────────────────

    /// <summary>递归清洗 tools/functionDeclarations 的 JSON Schema</summary>
    private void CleanToolSchemas(JsonObject body)
    {
        if (body["tools"] is not JsonArray tools) return;

        foreach (var tool in tools)
        {
            if (tool is not JsonObject toolObj) continue;

            var funcs = toolObj["functionDeclarations"]?.AsArray()
                     ?? toolObj["function_declarations"]?.AsArray();

            if (funcs == null) continue;

            foreach (var func in funcs)
            {
                if (func is JsonObject funcObj && funcObj["parameters"] is JsonObject paramsObj)
                    googleJsonSchemaCleaner.Clean(paramsObj);
            }
        }
    }

    /// <summary>修复 Gemini CLI 工具中 parametersJsonSchema → parameters 的字段名差异</summary>
    private static void FixGeminiCliTools(JsonObject body)
    {
        if (body["tools"] is not JsonArray tools) return;

        foreach (var tool in tools)
        {
            if (tool is not JsonObject toolObj) continue;

            var funcs = toolObj["functionDeclarations"] as JsonArray
                     ?? toolObj["function_declarations"] as JsonArray;

            if (funcs == null) continue;

            foreach (var func in funcs)
            {
                if (func is not JsonObject funcObj) continue;
                if (!funcObj.ContainsKey("parametersJsonSchema")) continue;

                var schema = funcObj["parametersJsonSchema"];
                funcObj.Remove("parametersJsonSchema");
                if (!funcObj.ContainsKey("parameters"))
                    funcObj["parameters"] = schema;
            }
        }
    }

    /// <summary>向 Gemini OAuth CLI 伪装请求中注入 session_id 和 user_prompt_id</summary>
    private static void InjectGeminiOAuthCliMetadata(
        JsonObject wrapper, JsonObject? payload, DownRequestContext down)
    {
        var sessionId = down.StickySessionId ?? Guid.NewGuid().ToString("D");

        if (payload != null && !payload.ContainsKey("session_id"))
            payload["session_id"] = sessionId;

        if (!wrapper.ContainsKey("user_prompt_id"))
            wrapper["user_prompt_id"] = $"{sessionId}########{down.PromptIndex}";
    }

    /// <summary>判断 Antigravity 请求类型（agent / web_search / image_gen）</summary>
    private static string DetermineRequestType(string modelId, JsonObject body)
    {
        if (modelId.Contains("image", StringComparison.OrdinalIgnoreCase)) return "image_gen";

        if (modelId.EndsWith("-online", StringComparison.OrdinalIgnoreCase)) return "web_search";

        if (body.TryGetPropertyValue("tools", out var toolsNode) && toolsNode is JsonArray toolsArray)
        {
            foreach (var tool in toolsArray)
            {
                if (tool is JsonObject toolObj &&
                    (toolObj.ContainsKey("googleSearch") || toolObj.ContainsKey("google_search_retrieval")))
                    return "web_search";
            }
        }

        return "agent";
    }

    // ── CLI 客户端检测 ─────────────────────────────────────────────────────────

    /// <summary>Gemini OAuth 场景的 CLI 检测（需验证 UserPromptId 格式 + session_id）</summary>
    private static bool IsGeminiOAuthCliClient(DownRequestContext down)
    {
        var userAgent = down.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !GeminiCliUARegex().IsMatch(userAgent))
            return false;

        if (!down.ExtractedProps.ContainsKey("google.is_cli"))
            return false;

        if (!down.ExtractedProps.TryGetValue("google.user_prompt_id", out var userPromptId) ||
            string.IsNullOrEmpty(userPromptId) ||
            !UserPromptIdRegex().IsMatch(userPromptId))
            return false;

        return (down.ExtractedProps.TryGetValue("google.request_session_id", out var sid1) && !string.IsNullOrEmpty(sid1)) ||
               (down.ExtractedProps.TryGetValue("public.conversation_id",  out var sid2) && !string.IsNullOrEmpty(sid2));
    }

    /// <summary>Gemini ApiKey 场景的 CLI 检测（简化：检查 UA + 特定 Header + CLI Prompt 标识）</summary>
    private static bool IsGeminiApiKeyCliClient(DownRequestContext down)
    {
        var userAgent = down.GetUserAgent();
        return !string.IsNullOrEmpty(userAgent) &&
               userAgent.StartsWith("GeminiCLI/", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-goog-api-client")) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-gemini-api-privileged-user-id")) &&
               down.ExtractedProps.ContainsKey("google.is_cli");
    }
}

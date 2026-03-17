using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Parsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

// 明确实现所有 BaseChatModelHandler 的抽象成员
public class GeminiAccountChatModelHandler(
    IHttpClientFactory httpClientFactory,
    GoogleJsonSchemaCleaner schemaCleaner,
    GoogleSignatureCleaner googleSignatureCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    IOptions<UsageLoggingOptions> loggingOptions,
    ILogger<GeminiAccountChatModelHandler> logger)
    : GoogleInternalChatModelHandlerBase(httpClientFactory, streamProcessor, signatureCache, loggingOptions, logger)
{
    private const string GeminiCliUserAgent = "GeminiCLI/0.33.1/{0} (win32; x64) google-api-nodejs-client/10.6.1";

    // Gemini CLI 临时目录正则匹配: .gemini/tmp/[64位哈希]
    private static readonly Regex GeminiCliTmpDirRegex = new(@"\.gemini/tmp/([A-Fa-f0-9]{64})", RegexOptions.Compiled);

    // Gemini CLI User-Agent 正则匹配
    private static readonly Regex GeminiCliUAPattern = new(@"^GeminiCLI/\d+\.\d+\.\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // user_prompt_id 格式正则: UUID + "########" + 数字
    private static readonly Regex UserPromptIdPattern = new(@"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}#{8}\d+$", RegexOptions.Compiled);

    public override bool Supports(ProviderPlatform platform)
    {
        return platform == ProviderPlatform.GEMINI_OAUTH;
    }

    public override async Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody)
    {
        var result = new ModelErrorAnalysisResult
        {
            ErrorType = ModelErrorType.Unknown,
            IsRetryableOnSameAccount = false,
            RequiresDowngrade = false,
            RetryAfter = null
        };

        // 检测 thoughtSignature 签名验证错误（400 Bad Request）
        if (statusCode == 400 && GoogleSignatureCleaner.IsSignatureError(responseBody))
        {
            result.ErrorType = ModelErrorType.SignatureError;
            result.IsRetryableOnSameAccount = true;
            result.RequiresDowngrade = true; // 触发降级重试
            logger.LogWarning("检测到 Gemini thoughtSignature 验证错误，建议降级重试");
            return result;
        }

        // 其他错误交给基类处理
        return await base.AnalyzeErrorAsync(statusCode, headers, responseBody);
    }

    public override void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId)
    {
        // 提取 ModelId
        // 1. 尝试从 URL 路径提取
        if (!string.IsNullOrEmpty(downContext.RelativePath))
        {
            var path = downContext.RelativePath;
            // 匹配 /models/{model}
            if (path.Contains("/models/"))
            {
                var parts = path.Split(new[] { "/models/" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var potentialModel = parts.Last();
                    var colonIndex = potentialModel.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        downContext.ModelId = potentialModel.Substring(0, colonIndex);
                    }
                    else
                    {
                        var slashIndex = potentialModel.IndexOf('/');
                        if (slashIndex > 0)
                        {
                            downContext.ModelId = potentialModel.Substring(0, slashIndex);
                        }
                        else
                        {
                            downContext.ModelId = potentialModel;
                        }
                    }
                }
            }
        }

        // 2. 尝试从 Body 提取
        if (string.IsNullOrEmpty(downContext.ModelId) &&
            downContext.BodyJsonNode is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("model", out var modelProp) &&
                modelProp is JsonValue modelValue &&
                modelValue.TryGetValue<string>(out var modelId))
            {
                downContext.ModelId = modelId;
            }
        }

        // ========== 提取 SessionHash ==========
        // 优先级 1: Gemini CLI 专用逻辑 (从 tmp 目录提取)
        // 该逻辑优先级最高，即使 Header 中有 session_id 也优先使用此特征，确保 CLI 上下文正确
        var match = downContext.SearchBodyPattern(GeminiCliTmpDirRegex, maxSearchLength: 50000);
        if (match.Success && match.Groups.Count >= 2)
        {
            var tmpDirHash = match.Groups[1].Value;

            // 获取 session_id Body
            string? sessionId = null;
            if (downContext.BodyJsonNode is JsonObject body)
            {
                // 获取内层 payload（已封装结构）
                var payload = body.ContainsKey("request")
                    ? body["request"] as JsonObject
                    : body;

                if (payload != null)
                {
                    // 优先级 1: session_id（直接使用，不混入上下文）
                    if (payload.TryGetPropertyValue("session_id", out var sessionIdNode) &&
                        sessionIdNode is JsonValue sessionIdValue)
                    {
                        sessionIdValue.TryGetValue<string>(out sessionId);
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                // 组合: session_id + ":" + tmp hash -> SHA256
                var combined = $"{sessionId.Trim()}:{tmpDirHash}";
                var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
                downContext.SessionHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            else
            {
                downContext.SessionHash = tmpDirHash;
            }
            return; // 匹配成功，直接返回
        }

        if (downContext.BodyJsonNode is JsonObject root)
        {

            // 优先级 2: conversation_id（直接使用，不混入上下文）
            if (root.TryGetPropertyValue("conversation_id", out var convIdNode) &&
                convIdNode is JsonValue convIdValue &&
                convIdValue.TryGetValue<string>(out var id))
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    downContext.SessionHash = id;
                    return;
                }
            }

            // 优先级 3: 只取第一条消息内容（实现会话粘性，混入上下文）
            if (root.TryGetPropertyValue("contents", out var contentsNode) &&
                contentsNode is JsonArray contents)
            {
                foreach (var contentNode in contents)
                {
                    var text = GeminiTextExtractor.ExtractTextFromParts(contentNode);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        downContext.SessionHash = GenerateSessionHashWithContext(
                            text,
                            downContext,
                            apiKeyId);
                        return;
                    }
                }
            }
        }
    }

    // ==================== IRequestTransformer ====================

    public override Task<TransformedRequestContext> TransformProtocolAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken = default)
    {
        var bodyJson = downContext.CloneBodyJson();
        JsonObject? finalBodyJson = null;

        if (bodyJson != null)
        {
            // 确定 payload 节点（检测是否已封装）
            JsonObject? payload;
            bool wrapped;

            if (bodyJson.ContainsKey("request") && bodyJson.ContainsKey("project"))
            {
                payload = bodyJson["request"] as JsonObject;
                wrapped = true;
            }
            else
            {
                payload = bodyJson;
                wrapped = false;
            }

            // 清洗 JSON Schema
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
                            {
                                schemaCleaner.Clean(paramsObj);
                            }
                        }
                    }
                }
            }

            // 包装请求
            var projectId = ConnectionOptions.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
            var modelId = downContext.ModelId ?? "gemini-3.0-flash-preview";

            if (!wrapped && payload != null)
            {
                payload.Remove("model");

                var wrapper = new JsonObject
                {
                    ["model"] = modelId,
                    ["project"] = projectId,
                    ["request"] = payload.DeepClone()
                };

                finalBodyJson = wrapper;
            }
            else if (wrapped)
            {
                // 已包装，确保 project 字段正确
                if (!string.IsNullOrEmpty(projectId))
                    bodyJson["project"] = projectId;
                finalBodyJson = bodyJson;
            }
            else
            {
                finalBodyJson = bodyJson;
            }
        }

        var transformedContext = new TransformedRequestContext
        {
            MappedModelId = downContext.ModelId, // GeminiAccount 不做模型映射
            BodyJson = finalBodyJson
        };

        return Task.FromResult(transformedContext);
    }

    // ==================== IRequestEnricher ====================

    public override void ApplyProxyEnhancements(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        var requestJson = transformedContext.BodyJson;
        if (requestJson == null) return;

        // 获取内层 payload（已封装结构）
        var payload = requestJson.ContainsKey("request")
            ? requestJson["request"] as JsonObject
            : requestJson;

        if (payload == null) return;

        // 两阶段签名降级处理（代理专属：测试场景不触发）
        if (downContext.DegradationLevel == 1)
        {
            googleSignatureCleaner.RemoveThoughtSignatures(payload);
            logger.LogWarning("应用降级级别 1: 移除 thoughtSignature");
        }
        else if (downContext.DegradationLevel >= 2)
        {
            googleSignatureCleaner.RemoveFunctionDeclarations(payload);
            logger.LogWarning("应用降级级别 2: 移除所有 FunctionDeclaration");
        }
    }

    public override Task<UpRequestContext> BuildHttpRequestAsync(
        DownRequestContext downContext,
        TransformedRequestContext transformedContext,
        CancellationToken cancellationToken = default)
    {
        // 构建 Headers
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {ConnectionOptions.Credential}"
        };

        // 伪装逻辑
        bool shouldMimic = ConnectionOptions.ShouldMimicOfficialClient;
        bool isGeminiCliClient = transformedContext.BodyJson != null && IsGeminiCliClient(downContext, transformedContext.BodyJson);

        if (shouldMimic && !isGeminiCliClient && transformedContext.BodyJson != null)
        {
            geminiSystemPromptInjector.InjectGeminiCliPrompt(transformedContext.BodyJson);
            InjectGeminiCliMetadata(transformedContext.BodyJson);
            ApplyGeminiCliHeaders(headers, transformedContext.MappedModelId);
        }

        // 构建 URL 路径（同原逻辑）
        var path = downContext.RelativePath ?? string.Empty;
        string operation = "streamGenerateContent";
        string query = "?alt=sse";

        if (!path.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase))
        {
            if (path.Contains(':'))
            {
                var parts = path.Split(':');
                if (parts.Length > 1) operation = parts[1].Split('?')[0];
            }
            if (operation != "streamGenerateContent") query = downContext.QueryString ?? "";
        }
        else
        {
            query = downContext.QueryString ?? "";
        }

        string newPath = path.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"/v1internal:{operation}";

        if (!newPath.StartsWith('/')) newPath = "/" + newPath;

        // 构建 HttpContent
        HttpContent? httpContent = null;
        string? bodyContent = null;
        if (transformedContext.BodyJson != null)
        {
            bodyContent = transformedContext.BodyJson.ToJsonString();
            httpContent = new ByteArrayContent(Encoding.UTF8.GetBytes(bodyContent));
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
        }

        return Task.FromResult(new UpRequestContext
        {
            Method = downContext.Method,
            BaseUrl = GetBaseUrl(),
            RelativePath = newPath,
            QueryString = query,
            Headers = headers,
            BodyContent = bodyContent,
            HttpContent = httpContent,
            MappedModelId = transformedContext.MappedModelId,
            SessionId = downContext.SessionHash
        });
    }


    public override async Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await LoadCodeAssistAsync(ConnectionOptions.Credential, cancellationToken);

            if (response == null)
            {
                return new ConnectionValidationResult(false, "LoadCodeAssist 返回空响应");
            }

            // 如果有 project_id，直接返回成功
            if (!string.IsNullOrEmpty(response.CloudaicompanionProject))
            {
                return new ConnectionValidationResult(true, ProjectId: response.CloudaicompanionProject);
            }

            // 没有 project_id，检查是否有不合格的原因
            if (response.IneligibleTiers != null && response.IneligibleTiers.Count > 0)
            {
                var ineligible = response.IneligibleTiers[0];
                var errorMessage = !string.IsNullOrEmpty(ineligible.ReasonMessage)
                    ? ineligible.ReasonMessage
                    : $"Account is not eligible for {ineligible.TierName ?? "Gemini Code Assist"}";

                logger.LogWarning("账户不符合 Code Assist 资格: {Error}", errorMessage);
                return new ConnectionValidationResult(false, errorMessage);
            }

            // 有 allowedTiers 但没有 project_id，说明账户已注册但未设置项目
            if (response.AllowedTiers != null && response.AllowedTiers.Count > 0)
            {
                var tierName = response.AllowedTiers[0].Name ?? "Gemini Code Assist";
                var errorMessage = $"Your account is registered for {tierName}, but no project_id is configured. Please create a project at https://console.cloud.google.com and configure it in your account settings.";

                logger.LogWarning("账户已注册但未配置 project_id");
                return new ConnectionValidationResult(false, errorMessage);
            }

            return new ConnectionValidationResult(false, "获取 Code Assist 项目失败：未返回 project_id");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "验证 Gemini Account 连接失败");
            return new ConnectionValidationResult(false, $"认证失败：{ex.Message}");
        }
    }

    public override async Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken cancellationToken = default)
    {
        var projectId = ConnectionOptions.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : null;
        return await FetchQuotaInternalAsync(ConnectionOptions.Credential, projectId, cancellationToken);
    }

    public override DownRequestContext CreateDebugDownContext(string modelId, string message)
    {
        var json = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray { new JsonObject { ["text"] = message } }
                }
            }
        };

        return new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = $"/v1beta/models/{modelId}:streamGenerateContent",
            QueryString = "?alt=sse",
            ModelId = modelId,
            Headers = new Dictionary<string, string>(),
            BodyBytes = Encoding.UTF8.GetBytes(json.ToJsonString()).AsMemory()
        };
    }

    // ==================== IResponseParser ====================

    public override ChatResponsePart? ParseChunk(string chunk)
    {
        return GeminiChatModelResponseParser.ParseChunkStatic(chunk);
    }

    public override ChatResponsePart ParseCompleteResponse(string responseBody)
    {
        return GeminiChatModelResponseParser.ParseCompleteResponseStatic(responseBody);
    }

    // ==================== Gemini CLI 客户端验证和伪装 ====================

    /// <summary>
    /// 判断请求是否来自真实的 Gemini CLI 客户端
    /// </summary>
    private static bool IsGeminiCliClient(DownRequestContext downContext, JsonObject requestJson)
    {
        var userAgent = downContext.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !GeminiCliUAPattern.IsMatch(userAgent))
            return false;

        // 获取内层 payload
        var payload = requestJson.ContainsKey("request")
            ? requestJson["request"] as JsonObject
            : requestJson;

        if (payload == null) return false;

        // 验证 user_prompt_id 格式
        if (!payload.TryGetPropertyValue("user_prompt_id", out var userPromptIdNode) ||
            userPromptIdNode is not JsonValue userPromptIdValue ||
            !userPromptIdValue.TryGetValue<string>(out var userPromptId) ||
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

        if (systemObj.TryGetPropertyValue("parts", out var partsNode) &&
            partsNode is JsonArray parts)
        {
            foreach (var part in parts)
            {
                if (part is JsonObject partObj &&
                    partObj.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text) &&
                    text.Contains("Gemini CLI", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 为 OAuth 模式补充 Gemini CLI 默认 Headers
    /// </summary>
    private static void ApplyGeminiCliHeaders(Dictionary<string, string> headers, string? modelId)
    {
        if (!headers.ContainsKey("accept"))
            headers["accept"] = "*/*";
        if (!headers.ContainsKey("user-agent"))
            headers["user-agent"] = string.Format(GeminiCliUserAgent, modelId ?? "gemini-3.0-flash-preview");
        if (!headers.ContainsKey("x-goog-api-client"))
            headers["x-goog-api-client"] = "gl-node/22.17.0";
        if (!headers.ContainsKey("content-type"))
            headers["content-type"] = "application/json";
    }

    /// <summary>
    /// 注入 Gemini CLI 元数据（user_prompt_id 和 session_id）
    /// </summary>
    private static void InjectGeminiCliMetadata(JsonObject requestJson)
    {
        // 注入 user_prompt_id: UUID + "########" + 随机数字
        if (!requestJson.ContainsKey("user_prompt_id"))
        {
            var randomDigit = Random.Shared.Next(0, 10);
            requestJson["user_prompt_id"] = $"{Guid.NewGuid():D}########{randomDigit}";
        }

        // 获取内层 payload
        var payload = requestJson.ContainsKey("request")
            ? requestJson["request"] as JsonObject
            : requestJson;

        if (payload == null) return;

        // 注入 session_id: UUID
        if (!payload.ContainsKey("session_id"))
        {
            payload["session_id"] = Guid.NewGuid().ToString("D");
        }
    }
}

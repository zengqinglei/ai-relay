using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Parsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.Json;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using Microsoft.Extensions.Logging;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// Antigravity 聊天模型客户端
/// </summary>
public sealed class AntigravityChatModelHandler(
    IHttpClientFactory httpClientFactory,
    IModelProvider modelProvider,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GoogleSignatureCleaner googleSignatureCleaner,
    AntigravityIdentityInjector antigravityIdentityInjector,
    GeminiToolsCleaner geminiToolsCleaner,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    IOptions<UsageLoggingOptions> loggingOptions,
    ILogger<AntigravityChatModelHandler> logger)
    : GoogleInternalChatModelHandlerBase(httpClientFactory, streamProcessor, signatureCache, loggingOptions, logger)
{
    private const string AntigravityUserAgent = "antigravity/1.20.5 windows/amd64";

    public override bool Supports(ProviderPlatform platform)
    {
        return platform == ProviderPlatform.ANTIGRAVITY;
    }

    public override string GetDefaultBaseUrl()
    {
        return "https://cloudcode-pa.googleapis.com";
    }

    public override string? GetFallbackBaseUrl(int statusCode)
    {
        if (statusCode == 429 ||
            statusCode == 408 ||
            statusCode == 404 ||
            (statusCode >= 500 && statusCode < 600))
        {
            return "https://daily-cloudcode-pa.sandbox.googleapis.com";
        }
        return null;
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

        // 1. 签名错误 (400 Bad Request + 特定 Body)
        if (statusCode == 400)
        {
            if (GoogleSignatureCleaner.IsSignatureError(responseBody))
            {
                result.ErrorType = ModelErrorType.SignatureError;
                result.IsRetryableOnSameAccount = true;
                result.RequiresDowngrade = true; // 关键：告诉中间件下次要降级
                return result;
            }

            result.ErrorType = ModelErrorType.BadRequest;
            return result;
        }

        // 2. 其他错误 (RateLimit, ServerError, etc.) 交给基类处理
        // GoogleInternalChatModelHandlerBase 会处理 Google 特有的 JSON 错误格式
        // BaseChatModelHandler 会处理通用的 Retry-After 头和 Body
        return await base.AnalyzeErrorAsync(statusCode, headers, responseBody);
    }

    public override void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId)
    {
        // 提取 ModelId
        if (downContext.BodyJsonNode is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("model", out var modelProp) &&
                modelProp is JsonValue modelValue &&
                modelValue.TryGetValue<string>(out var modelId))
            {
                downContext.ModelId = modelId;
            }
        }

        // ========== 提取 SessionHash ==========
        // Antigravity 不从 Header 提取（Header 仅用于日志），完全依赖 Body
        if (downContext.BodyJsonNode is JsonObject root)
        {
            // 优先级 1: conversation_id（直接使用，不混入上下文）
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

            // 优先级 2: 只取第一条消息内容（实现会话粘性，混入上下文）
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
        string? mappedModelId;
        JsonObject? finalBodyJson;

        if (downContext.BodyJsonNode is not JsonObject bodyJson)
        {
            mappedModelId = downContext.ModelId ?? "gemini-2.0-flash-exp";
            finalBodyJson = null;
        }
        else
        {
            var clonedBody = downContext.CloneBodyJson() ?? new JsonObject();

            // 模型映射
            mappedModelId = string.IsNullOrEmpty(downContext.ModelId)
                ? "gemini-2.0-flash-exp"
                : modelProvider.GetAntigravityMappedModel(downContext.ModelId);

            // 协议必需：注入 Antigravity 特有字段
            antigravityIdentityInjector.EnsureAntigravityIdentity(clonedBody);
            geminiToolsCleaner.FixGeminiCliTools(clonedBody);

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
            var requestType = DetermineRequestType(mappedModelId, clonedBody);
            var projectId = ConnectionOptions.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";

            var wrapper = new JsonObject
            {
                ["project"] = projectId,
                ["requestId"] = $"agent-{Guid.NewGuid()}",
                ["userAgent"] = "antigravity",
                ["requestType"] = requestType,
                ["model"] = mappedModelId,
                ["request"] = clonedBody
            };

            finalBodyJson = wrapper;
            logger.LogDebug("已构建 Antigravity 请求: Model={Model}, Type={Type}", mappedModelId, requestType);
        }

        var transformedContext = new TransformedRequestContext
        {
            MappedModelId = mappedModelId,
            BodyJson = finalBodyJson
        };

        // 透传 anthropic-* headers 到 ProtocolHeaders
        if (downContext.Headers.TryGetValue("anthropic-version", out var version) && !string.IsNullOrWhiteSpace(version))
            transformedContext.ProtocolHeaders["anthropic-version"] = version;
        if (downContext.Headers.TryGetValue("anthropic-beta", out var beta) && !string.IsNullOrWhiteSpace(beta))
            transformedContext.ProtocolHeaders["anthropic-beta"] = beta;

        return Task.FromResult(transformedContext);
    }

    // ==================== IRequestEnricher ====================

    public override void ApplyProxyEnhancements(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        var requestJson = transformedContext.BodyJson;
        if (requestJson == null) return;

        // 内层 payload（已封装结构）
        var payload = requestJson.ContainsKey("request")
            ? requestJson["request"] as JsonObject
            : requestJson;

        if (payload == null) return;

        // 两阶段签名降级处理（代理专属）
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

        // 签名注入（仅当未降级且 SessionHash 存在时）
        if (downContext.DegradationLevel == 0 && !string.IsNullOrEmpty(downContext.SessionHash))
        {
            googleSignatureCleaner.InjectCachedSignature(payload, downContext.SessionHash);
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
            ["User-Agent"] = AntigravityUserAgent,
            ["Authorization"] = $"Bearer {ConnectionOptions.Credential}",
            ["Content-Type"] = "application/json"
        };

        // 透传 anthropic-* headers（从 ProtocolHeaders）
        foreach (var kvp in transformedContext.ProtocolHeaders)
            headers[kvp.Key] = kvp.Value;

        // 构造 URL
        var path = downContext.RelativePath ?? string.Empty;
        string operation = "streamGenerateContent";
        string query = "?alt=sse";

        if (!path.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase))
        {
            if (path.Contains(':'))
            {
                var parts = path.Split(':');
                if (parts.Length > 1)
                {
                    var potentialOp = parts[1].Split('?')[0];
                    if (!string.IsNullOrEmpty(potentialOp))
                        operation = potentialOp;
                }
            }
            if (operation != "streamGenerateContent")
                query = downContext.QueryString ?? "";
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
            bodyContent = transformedContext.BodyJson.ToJsonString(JsonOptions.Compact);
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

                return new ConnectionValidationResult(false, errorMessage);
            }

            // 有 allowedTiers 但没有 project_id，说明账户已注册但未设置项目
            if (response.AllowedTiers != null && response.AllowedTiers.Count > 0)
            {
                var tierName = response.AllowedTiers[0].Name ?? "Gemini Code Assist";
                var errorMessage = $"Your account is registered for {tierName}, but no project_id is configured. Please create a project at https://console.cloud.google.com and configure it in your account settings.";

                return new ConnectionValidationResult(false, errorMessage);
            }

            return new ConnectionValidationResult(false, "获取 Code Assist 项目失败：未返回 project_id");
        }
        catch (Exception ex)
        {
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


    private string DetermineRequestType(string modelId, JsonObject requestJson)
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

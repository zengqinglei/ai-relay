using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using Microsoft.Extensions.Logging;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public partial class ClaudeChatModelHandler(
    ClaudeRequestCleaner claudeRequestCleaner,
    ClaudeThinkingCleaner claudeThinkingCleaner,
    ClaudeCacheControlCleaner claudeCacheControlCleaner,
    ClaudeSystemPromptInjector claudeSystemPromptInjector,
    IModelProvider modelProvider,
    IHttpClientFactory httpClientFactory,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    IOptions<UsageLoggingOptions> loggingOptions,
    ILogger<ClaudeChatModelHandler> logger)
    : BaseChatModelHandler(httpClientFactory, streamProcessor, signatureCache, loggingOptions, logger)
{
    // Claude Code 客户端 User-Agent
    private const string ClaudeCodeUserAgent = "claude-cli/2.1.74 (external, cli)";

    // 白名单 headers
    private static readonly HashSet<string> AllowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept",
        "x-stainless-retry-count",
        "x-stainless-timeout",
        "x-stainless-lang",
        "x-stainless-package-version",
        "x-stainless-os",
        "x-stainless-arch",
        "x-stainless-runtime",
        "x-stainless-runtime-version",
        "x-stainless-helper-method",
        "anthropic-dangerous-direct-browser-access",
        "anthropic-version",
        "x-app",
        "anthropic-beta",
        "accept-language",
        "sec-fetch-mode",
        "user-agent",
        "content-type"
    };
    // ==================== IChatModelHandler ====================

    public override bool Supports(ProviderPlatform platform)
    {
        return platform is ProviderPlatform.CLAUDE_OAUTH or ProviderPlatform.CLAUDE_APIKEY;
    }

    public override string GetDefaultBaseUrl()
    {
        return "https://api.anthropic.com";
    }

    public override Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ConnectionValidationResult(true));
    }

    public override Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AccountQuotaInfo?>(null);
    }

    public override DownRequestContext CreateDebugDownContext(string modelId, string message)
    {
        var json = new JsonObject
        {
            ["model"] = modelId,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = message
                }
            },
            ["max_tokens"] = 1024,
            ["stream"] = true
        };

        return new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = "/v1/messages",
            ModelId = modelId,
            BodyBytes = Encoding.UTF8.GetBytes(json.ToJsonString()).AsMemory(),
            Headers = new Dictionary<string, string>()
        };
    }

    // ==================== IRequestTransformer ====================

    public override void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId)
    {
        // 提取 ModelId（使用懒加载的 BodyJsonNode）
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
        // Claude 平台不从 Header 提取，完全依赖 Body（更安全，防止伪造）
        if (downContext.BodyJsonNode is JsonObject root)
        {
            // 优先级 1: metadata.user_id 中提取 session_xxx（直接使用，不混入上下文）
            if (root.TryGetPropertyValue("metadata", out var metadataNode) &&
                metadataNode is JsonObject metadata &&
                metadata.TryGetPropertyValue("user_id", out var userIdNode) &&
                userIdNode is JsonValue userIdValue &&
                userIdValue.TryGetValue<string>(out var userIdStr))
            {
                if (!string.IsNullOrWhiteSpace(userIdStr))
                {
                    // 提取 session_xxx 格式的 ID
                    if (userIdStr.StartsWith("session_", StringComparison.OrdinalIgnoreCase))
                    {
                        var sessionId = userIdStr.Substring("session_".Length);
                        if (!string.IsNullOrWhiteSpace(sessionId))
                        {
                            downContext.SessionHash = sessionId;
                            return;
                        }
                    }

                    // 如果不是 session_ 格式，直接使用
                    downContext.SessionHash = userIdStr;
                    return;
                }
            }

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

            // 优先级 3: cache_control: ephemeral 内容（Prompt Caching 支持，混入上下文）
            var cacheableContent = ExtractCacheableContent(root);
            if (!string.IsNullOrWhiteSpace(cacheableContent))
            {
                downContext.SessionHash = GenerateSessionHashWithContext(
                    cacheableContent,
                    downContext,
                    apiKeyId);
                return;
            }

            // 优先级 4: 只取第一条消息内容（实现会话粘性，混入上下文）
            if (root.TryGetPropertyValue("messages", out var messagesNode) &&
                messagesNode is JsonArray messages)
            {
                foreach (var messageNode in messages)
                {
                    var text = ExtractTextFromContent(messageNode);
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

    private static string ExtractTextFromContent(JsonNode? message)
    {
        if (message is not JsonObject messageObj ||
            !messageObj.TryGetPropertyValue("content", out var contentNode))
            return string.Empty;

        if (contentNode is JsonValue contentValue &&
            contentValue.TryGetValue<string>(out var contentStr))
        {
            return contentStr ?? string.Empty;
        }

        if (contentNode is JsonArray contentArray)
        {
            var sb = new StringBuilder();
            foreach (var block in contentArray)
            {
                if (block is JsonObject blockObj &&
                    blockObj.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue &&
                    typeValue.TryGetValue<string>(out var type) &&
                    type == "text" &&
                    blockObj.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text))
                {
                    sb.Append(text);
                }
            }
            return sb.ToString();
        }

        return string.Empty;
    }

    // ==================== IRequestTransformer ====================

    public override Task<TransformedRequestContext> TransformProtocolAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken = default)
    {
        var requestJson = downContext.CloneBodyJson();

        // 模型规范化（短 ID → 完整 ID）
        string? mappedModelId = downContext.ModelId;
        if (requestJson != null && requestJson.TryGetPropertyValue("model", out var modelNode) && modelNode != null)
        {
            var model = modelNode.GetValue<string>();
            var normalized = modelProvider.GetClaudeMappedModel(model);
            mappedModelId = normalized;
            if (normalized != model)
                requestJson["model"] = normalized;
        }
        else if (!string.IsNullOrEmpty(mappedModelId))
        {
            mappedModelId = modelProvider.GetClaudeMappedModel(mappedModelId);
        }

        var transformedContext = new TransformedRequestContext
        {
            MappedModelId = mappedModelId,
            BodyJson = requestJson
        };

        return Task.FromResult(transformedContext);
    }

    // ==================== IRequestEnricher ====================

    public override void ApplyProxyEnhancements(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        bool isOAuthAccount = ConnectionOptions.Platform == ProviderPlatform.CLAUDE_OAUTH;

        var requestJson = transformedContext.BodyJson;
        if (requestJson == null) return;

        // [1] OAuth 专属：过滤黑名单系统提示词
        if (isOAuthAccount)
        {
            claudeRequestCleaner.FilterSystemBlocksByPrefix(requestJson);
        }

        // [2] 两阶段 Thinking 降级处理
        if (downContext.DegradationLevel == 1)
        {
            if (claudeThinkingCleaner.FilterThinkingBlocks(requestJson))
                logger.LogWarning("应用降级级别 1: 移除 thinking 配置并转换 thinking 块");
        }
        else if (downContext.DegradationLevel >= 2)
        {
            if (claudeThinkingCleaner.FilterSignatureSensitiveBlocks(requestJson))
                logger.LogWarning("应用降级级别 2: 转换所有签名敏感块（thinking + tools）");
        }

        // [3] 强制执行 cache_control 块数量限制（最多 4 个）
        claudeCacheControlCleaner.EnforceCacheControlLimit(requestJson);
    }

    public override Task<UpRequestContext> BuildHttpRequestAsync(
        DownRequestContext downContext,
        TransformedRequestContext transformedContext,
        CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>();
        var requestJson = transformedContext.BodyJson;

        // 白名单过滤下游 Headers
        foreach (var kvp in downContext.Headers)
        {
            if (AllowedHeaders.Contains(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                headers[kvp.Key] = kvp.Value;
        }

        // 透传协议特定 Headers
        foreach (var kvp in transformedContext.ProtocolHeaders)
            headers[kvp.Key] = kvp.Value;

        // 确保必要 headers 存在
        if (!headers.ContainsKey("anthropic-version"))
            headers["anthropic-version"] = "2023-06-01";
        if (!headers.ContainsKey("content-type"))
            headers["content-type"] = "application/json";

        bool isOAuthAccount = ConnectionOptions.Platform == ProviderPlatform.CLAUDE_OAUTH;

        // 伪装逻辑（统一入口）
        bool shouldMimic = ConnectionOptions.ShouldMimicOfficialClient;
        bool isClaudeCodeClient = requestJson != null && IsClaudeCodeClient(downContext, requestJson);
        bool isHaikuModel = !string.IsNullOrEmpty(transformedContext.MappedModelId) &&
                            transformedContext.MappedModelId.Contains("haiku", StringComparison.OrdinalIgnoreCase);

        if (shouldMimic && !isClaudeCodeClient && !isHaikuModel && requestJson != null)
        {
            claudeSystemPromptInjector.InjectClaudeCodePrompt(requestJson);
            InjectClaudeCodeMetadata(requestJson);
            ApplyClaudeCodeHeaders(headers);
        }

        // 必要的认证信息
        if (isOAuthAccount)
        {

            if (!headers.ContainsKey("anthropic-beta"))
                headers["anthropic-beta"] = BuildClaudeOAuthBetaHeaderFromTransformed(downContext, transformedContext);

            headers["authorization"] = $"Bearer {ConnectionOptions.Credential}";
        }
        else
        {
            headers.Remove("authorization");
            headers.Remove("cookie");
            headers["x-api-key"] = ConnectionOptions.Credential;

            if (!headers.ContainsKey("anthropic-beta"))
                headers["anthropic-beta"] = BuildClaudeApiKeyBetaHeaderFromTransformed(transformedContext);
        }

        // 构建路径
        var relativePath = downContext.RelativePath;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/')) relativePath = "/" + relativePath;

        // 构建 QueryString（追加 beta=true）
        var queryString = downContext.QueryString ?? string.Empty;
        if (string.IsNullOrEmpty(queryString))
        {
            queryString = "?beta=true";
        }
        else if (!queryString.Contains("beta=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = queryString.Contains('?') ? "&" : "?";
            queryString = $"{queryString}{separator}beta=true";
        }

        // 构建 HttpContent
        HttpContent? httpContent = null;
        string? bodyContent = null;
        if (transformedContext.BodyJson != null)
        {
            bodyContent = transformedContext.BodyJson.ToJsonString();
            var bodyBytes = Encoding.UTF8.GetBytes(bodyContent);
            httpContent = new ByteArrayContent(bodyBytes);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return Task.FromResult(new UpRequestContext
        {
            Method = downContext.Method,
            BaseUrl = GetBaseUrl(),
            RelativePath = relativePath,
            QueryString = queryString,
            Headers = headers,
            BodyContent = bodyContent,
            HttpContent = httpContent,
            MappedModelId = transformedContext.MappedModelId,
            SessionId = downContext.SessionHash
        });
    }



    /// <summary>
    /// 为 OAuth 模式补充 Claude CLI 默认 Headers（仅在客户端未提供时）
    /// 遵循"优先透传，缺失时补充"原则
    /// </summary>
    private static void ApplyClaudeCodeHeaders(Dictionary<string, string> headers)
    {
        if (!headers.ContainsKey("accept"))
            headers["accept"] = "application/json";
        if (!headers.ContainsKey("user-agent"))
            headers["user-agent"] = ClaudeCodeUserAgent;
        if (!headers.ContainsKey("x-stainless-lang"))
            headers["x-stainless-lang"] = "js";
        if (!headers.ContainsKey("x-stainless-package-version"))
            headers["x-stainless-package-version"] = "0.74.0";
        if (!headers.ContainsKey("x-stainless-os"))
            headers["x-stainless-os"] = "Windows";
        if (!headers.ContainsKey("x-stainless-arch"))
            headers["x-stainless-arch"] = "x64";
        if (!headers.ContainsKey("x-stainless-runtime"))
            headers["x-stainless-runtime"] = "node";
        if (!headers.ContainsKey("x-stainless-runtime-version"))
            headers["x-stainless-runtime-version"] = "v22.17.0";
        if (!headers.ContainsKey("x-stainless-retry-count"))
            headers["x-stainless-retry-count"] = "0";
        if (!headers.ContainsKey("x-stainless-timeout"))
            headers["x-stainless-timeout"] = "600";
        if (!headers.ContainsKey("x-app"))
            headers["x-app"] = "cli";
        if (!headers.ContainsKey("anthropic-dangerous-direct-browser-access"))
            headers["anthropic-dangerous-direct-browser-access"] = "true";
        if (!headers.ContainsKey("accept-language"))
            headers["accept-language"] = "*";
        if (!headers.ContainsKey("sec-fetch-mode"))
            headers["sec-fetch-mode"] = "cors";
    }

    /// <summary>
    /// 构建 OAuth 模式的 Beta Header（根据客户端类型和模型类型）
    /// </summary>
    private static string BuildClaudeOAuthBetaHeaderFromTransformed(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        bool isClaudeCodeClient = IsClaudeCodeClient(downContext, transformedContext.BodyJson);
        bool isHaikuModel = !string.IsNullOrEmpty(transformedContext.MappedModelId) &&
                            transformedContext.MappedModelId.Contains("haiku", StringComparison.OrdinalIgnoreCase);

        // Haiku 不需要 claude-code beta
        if (isHaikuModel)
            return "oauth-2025-04-20,interleaved-thinking-2025-05-14";

        // 所有非 Haiku OAuth 请求均需包含 claude-code beta（OAuth credentials 的 scope 绑定到 Claude Code）
        return isClaudeCodeClient
            ? "claude-code-20250219,context-1m-2025-08-07,interleaved-thinking-2025-05-14,redact-thinking-2026-02-12,context-management-2025-06-27,prompt-caching-scope-2026-01-05,effort-2025-11-24"
            : "claude-code-20250219,oauth-2025-04-20,interleaved-thinking-2025-05-14";
    }

    /// <summary>
    /// 构建 API Key 模式的 Beta Header（根据模型类型）
    /// </summary>
    private static string BuildClaudeApiKeyBetaHeaderFromTransformed(TransformedRequestContext transformedContext)
    {
        bool isHaikuModel = !string.IsNullOrEmpty(transformedContext.MappedModelId) &&
                            transformedContext.MappedModelId.Contains("haiku", StringComparison.OrdinalIgnoreCase);

        return isHaikuModel
            ? "interleaved-thinking-2025-05-14"
            : "claude-code-20250219,interleaved-thinking-2025-05-14,fine-grained-tool-streaming-2025-05-14";
    }

    // ==================== Error Analysis ====================

    public override Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
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

        // 检测 thinking 签名错误（400 状态码）
        if (statusCode == 400 && ClaudeThinkingCleaner.IsThinkingBlockSignatureError(responseBody))
        {
            result.ErrorType = ModelErrorType.SignatureError;
            result.IsRetryableOnSameAccount = true;
            result.RequiresDowngrade = true;
            logger.LogWarning("检测到 Claude thinking 签名错误，建议降级重试");
            return Task.FromResult(result);
        }

        // 其他错误使用基类默认逻辑
        return base.AnalyzeErrorAsync(statusCode, headers, responseBody);
    }

    // ==================== IResponseParser ====================

    public override ChatResponsePart? ParseChunk(string chunk)
    {
        return ClaudeChatModelResponseParser.ParseChunkStatic(chunk);
    }

    public override ChatResponsePart ParseCompleteResponse(string responseBody)
    {
        return ClaudeChatModelResponseParser.ParseCompleteResponseStatic(responseBody);
    }

    // ==================== Claude Code 客户端验证（对齐 sub2api）====================

    private static readonly Regex ClaudeCodeUAPattern = new(@"^claude-cli/\d+\.\d+\.\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UserIdPattern = new(@"^user_[a-fA-F0-9]{64}_account__session_[\w-]+$", RegexOptions.Compiled);

    private const double SystemPromptThreshold = 0.5;

    private static readonly string[] ClaudeCodeSystemPrompts =
    [
        "You are Claude Code, Anthropic's official CLI for Claude.",
        "You are a Claude agent, built on Anthropic's Claude Agent SDK.",
        "You are Claude Code, Anthropic's official CLI for Claude, running within the Claude Agent SDK.",
        "You are a file search specialist for Claude Code, Anthropic's official CLI for Claude.",
        "You are a helpful AI assistant tasked with summarizing conversations.",
        "You are an interactive CLI tool that helps users"
    ];

    private static bool IsClaudeCodeClient(DownRequestContext downContext, JsonObject? requestJson)
    {
        var userAgent = downContext.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !ClaudeCodeUAPattern.IsMatch(userAgent))
            return false;

        var isMessagesPath = downContext.RelativePath?.Contains("messages", StringComparison.OrdinalIgnoreCase) == true;
        if (!isMessagesPath || requestJson == null)
            return true;

        return HasClaudeCodeSystemPrompt(requestJson) &&
               !string.IsNullOrEmpty(downContext.Headers.GetValueOrDefault("x-app")) &&
               !string.IsNullOrEmpty(downContext.Headers.GetValueOrDefault("anthropic-beta")) &&
               !string.IsNullOrEmpty(downContext.Headers.GetValueOrDefault("anthropic-version")) &&
               ValidateMetadataUserId(requestJson);
    }

    private static bool HasClaudeCodeSystemPrompt(JsonObject requestJson)
    {
        if (!requestJson.TryGetPropertyValue("system", out var systemNode) || systemNode is not JsonArray systemArray)
            return false;

        foreach (var entry in systemArray)
        {
            if (entry is JsonObject entryObj &&
                entryObj.TryGetPropertyValue("text", out var textNode) &&
                textNode is JsonValue textValue &&
                textValue.TryGetValue<string>(out var text) &&
                !string.IsNullOrEmpty(text) &&
                BestSimilarityScore(text) >= SystemPromptThreshold)
                return true;
        }
        return false;
    }

    private static bool ValidateMetadataUserId(JsonObject requestJson)
    {
        return requestJson.TryGetPropertyValue("metadata", out var metadataNode) &&
               metadataNode is JsonObject metadata &&
               metadata.TryGetPropertyValue("user_id", out var userIdNode) &&
               userIdNode is JsonValue userIdValue &&
               userIdValue.TryGetValue<string>(out var userId) &&
               !string.IsNullOrEmpty(userId) &&
               UserIdPattern.IsMatch(userId);
    }

    private static double BestSimilarityScore(string text)
    {
        var normalizedText = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var bestScore = 0.0;

        foreach (var template in ClaudeCodeSystemPrompts)
        {
            var normalizedTemplate = string.Join(" ", template.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var score = DiceCoefficient(normalizedText, normalizedTemplate);
            if (score > bestScore) bestScore = score;
        }
        return bestScore;
    }

    private static double DiceCoefficient(string a, string b)
    {
        if (a == b) return 1.0;
        if (a.Length < 2 || b.Length < 2) return 0.0;

        var bigramsA = GetBigrams(a);
        var bigramsB = GetBigrams(b);
        if (bigramsA.Count == 0 || bigramsB.Count == 0) return 0.0;

        var intersection = bigramsA.Sum(kvp => bigramsB.TryGetValue(kvp.Key, out var countB) ? Math.Min(kvp.Value, countB) : 0);
        return 2.0 * intersection / (bigramsA.Values.Sum() + bigramsB.Values.Sum());
    }

    private static Dictionary<string, int> GetBigrams(string s)
    {
        var bigrams = new Dictionary<string, int>();
        var lower = s.ToLowerInvariant();
        for (var i = 0; i < lower.Length - 1; i++)
        {
            var bigram = lower.Substring(i, 2);
            bigrams[bigram] = bigrams.GetValueOrDefault(bigram) + 1;
        }
        return bigrams;
    }

    private static void InjectClaudeCodeMetadata(JsonObject requestJson)
    {
        if (!requestJson.ContainsKey("metadata"))
            requestJson["metadata"] = new JsonObject();

        if (requestJson["metadata"] is JsonObject metadata && !metadata.ContainsKey("user_id"))
        {
            var randomBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
            var hex64 = Convert.ToHexString(randomBytes).ToLowerInvariant();
            metadata["user_id"] = $"user_{hex64}_account__session_{Guid.NewGuid():N}";
        }
    }
}

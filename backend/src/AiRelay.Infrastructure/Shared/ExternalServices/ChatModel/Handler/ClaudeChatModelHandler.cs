using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public class ClaudeChatModelHandler(
    ChatModelConnectionOptions options,
    ClaudeRequestCleaner claudeRequestCleaner,
    ClaudeThinkingCleaner claudeThinkingCleaner,
    ClaudeCacheControlCleaner claudeCacheControlCleaner,
    ClaudeSystemPromptInjector claudeSystemPromptInjector,
    IModelProvider modelProvider,
    AccountFingerprintDomainService fingerprintDomainService,
    IClaudeCodeClientDetector clientDetector,
    IHttpClientFactory httpClientFactory,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    ILogger<ClaudeChatModelHandler> logger)
    : BaseChatModelHandler(options, httpClientFactory, streamProcessor, signatureCache, logger)
{
    public override bool Supports(ProviderPlatform platform) =>
        platform is ProviderPlatform.CLAUDE_OAUTH or ProviderPlatform.CLAUDE_APIKEY;

    protected override bool IsChatApiPath(string? path) =>
        path != null && path.Contains("/v1/messages", StringComparison.OrdinalIgnoreCase);

    public override Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default) =>
        Task.FromResult(new ConnectionValidationResult(true));

    public override Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken ct = default) =>
        Task.FromResult<AccountQuotaInfo?>(null);

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
            Headers = []
        };
    }

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        // 非聊天路径：仅 Header 认证
        var isChatApi = IsChatApiPath(down.RelativePath);
        var processors = new List<IRequestProcessor> { new ClaudeUrlProcessor(isChatApi, _options) };
        if (!isChatApi)
        {
            processors.Add(new ClaudeHeaderProcessor(_options, clientDetector));
        }
        else
        {
            processors.AddRange(
                new ClaudeModelIdMappingProcessor(modelProvider),
                new ClaudeHeaderProcessor(_options, clientDetector), // 必须放在ModelMapping之后，以便正确识别是否官方客户端和Haiku模型
                new ClaudeRequestBodyProcessor(
                    _options, claudeRequestCleaner, claudeCacheControlCleaner,
                    claudeSystemPromptInjector, clientDetector));

            // Claude OAuth 专属：fingerprint user_id 注入（异步，需要 AppService）
            // 仅 OAuth 且非 batches 路由
            if (_options.Platform == ProviderPlatform.CLAUDE_OAUTH
                && !down.RelativePath.Contains("/batches", StringComparison.OrdinalIgnoreCase))
            {
                processors.Add(new ClaudeMetadataInjectProcessor(_options, fingerprintDomainService));
            }

            // 降级 Processor 仅在需要时加入
            if (degradationLevel > 0)
                processors.Add(new ClaudeDegradationProcessor(degradationLevel, claudeThinkingCleaner, logger));
        }
        return processors;
    }

    // ── ExtractModelInfo

    public override void ExtractModelInfo(DownRequestContext down, Guid apiKeyId)
    {
        // 提取 ModelId
        if (down.BodyJsonNode is JsonObject obj &&
            obj.TryGetPropertyValue("model", out var modelProp) &&
            modelProp is JsonValue modelValue &&
            modelValue.TryGetValue<string>(out var modelId))
        {
            down.ModelId = modelId;
        }

        // 提取 SessionHash
        if (down.BodyJsonNode is not JsonObject root) return;

        // 优先级 1: metadata.user_id
        if (root.TryGetPropertyValue("metadata", out var metadataNode) &&
            metadataNode is JsonObject metadata &&
            metadata.TryGetPropertyValue("user_id", out var userIdNode) &&
            userIdNode is JsonValue userIdValue &&
            userIdValue.TryGetValue<string>(out var userIdStr) &&
            !string.IsNullOrWhiteSpace(userIdStr))
        {
            if (userIdStr.StartsWith("session_", StringComparison.OrdinalIgnoreCase))
            {
                var sessionId = userIdStr.Substring("session_".Length);
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    down.SessionHash = sessionId;
                    return;
                }
            }
            down.SessionHash = userIdStr;
            return;
        }

        // 优先级 2: conversation_id
        if (root.TryGetPropertyValue("conversation_id", out var convIdNode) &&
            convIdNode is JsonValue convIdValue &&
            convIdValue.TryGetValue<string>(out var id) &&
            !string.IsNullOrWhiteSpace(id))
        {
            down.SessionHash = id;
            return;
        }

        // 优先级 3: cache_control ephemeral 内容
        var cacheableContent = ExtractCacheableContent(root);
        if (!string.IsNullOrWhiteSpace(cacheableContent))
        {
            down.SessionHash = GenerateSessionHashWithContext(cacheableContent, down, apiKeyId);
            return;
        }

        // 优先级 4: 第一条消息内容
        if (root.TryGetPropertyValue("messages", out var messagesNode) &&
            messagesNode is JsonArray messages)
        {
            foreach (var messageNode in messages)
            {
                var text = ExtractTextFromContent(messageNode);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    down.SessionHash = GenerateSessionHashWithContext(text, down, apiKeyId);
                    return;
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
            return contentStr ?? string.Empty;

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

    // ── Error Analysis

    public override Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody)
    {
        if (statusCode == 400 && ClaudeThinkingCleaner.IsThinkingBlockSignatureError(responseBody))
        {
            logger.LogWarning("检测到 Claude thinking 签名错误，建议降级重试");
            return Task.FromResult(new ModelErrorAnalysisResult
            {
                ErrorType = ModelErrorType.SignatureError,
                IsRetryableOnSameAccount = true,
                RequiresDowngrade = true,
                RetryAfter = null
            });
        }

        return base.AnalyzeErrorAsync(statusCode, headers, responseBody);
    }

    // ── IResponseParser

    public override ChatResponsePart? ParseChunk(string chunk) =>
        ClaudeChatModelResponseParser.ParseChunkStatic(chunk);

    public override ChatResponsePart ParseCompleteResponse(string responseBody) =>
        ClaudeChatModelResponseParser.ParseCompleteResponseStatic(responseBody);
}

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
using System.Text.Json;

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

    public override async Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default)
    {
        // 仅 ApiKey 支持
        if (Options.Platform != ProviderPlatform.CLAUDE_APIKEY)
            return null;

        try
        {
            // 1. 构造 DownRequestContext（GET /v1/models）
            var down = new DownRequestContext
            {
                Method = HttpMethod.Get,
                RelativePath = "/v1/models",
                Headers = []
            };

            // 2. 通过 Processor 链处理（复用 Header 处理逻辑）
            var up = await ProcessRequestContextAsync(down, 0, ct);

            // 3. 发送请求
            using var response = await ProxyRequestAsync(up, ct);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Claude 上游模型拉取失败: {StatusCode}", response.StatusCode);
                return null;
            }

            // 4. 解析响应
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var models = new List<ModelOption>();

            if (doc.RootElement.TryGetProperty("data", out var dataArray))
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idProp))
                    {
                        var id = idProp.GetString();
                        if (!string.IsNullOrEmpty(id) && id.StartsWith("claude-"))
                        {
                            var displayName = item.TryGetProperty("display_name", out var nameProp)
                                ? nameProp.GetString() ?? id
                                : id;
                            models.Add(new ModelOption(displayName, id));
                        }
                    }
                }
            }

            Logger.LogInformation("Claude 上游拉取成功: {Count} 个模型", models.Count);
            return models.Count > 0 ? models : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Claude 上游模型拉取异常");
            return null;
        }
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
            Headers = []
        };
    }

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return [
            new ClaudeModelIdMappingProcessor(modelProvider, Options),
            new ClaudeUrlProcessor(Options),
            new ClaudeHeaderProcessor(Options, clientDetector),
            new ClaudeRequestBodyProcessor(
                Options,
                claudeRequestCleaner,
                claudeCacheControlCleaner,
                claudeSystemPromptInjector,
                clientDetector),
            new ClaudeMetadataInjectProcessor(Options, fingerprintDomainService),
            new ClaudeDegradationProcessor(degradationLevel, claudeThinkingCleaner, Logger)
        ];
    }

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
            Logger.LogWarning("检测到 Claude thinking 签名错误，建议降级重试");
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

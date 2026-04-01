using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Response;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Response.Claude;
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
    IClaudeCodeClientDetector clientDetector,
    IHttpClientFactory httpClientFactory,
    ISignatureCache signatureCache,
    ILogger<ClaudeChatModelHandler> logger)
    : BaseChatModelHandler(options, httpClientFactory, signatureCache, logger)
{
    public override bool Supports(ProviderPlatform platform) =>
        platform is ProviderPlatform.CLAUDE_OAUTH or ProviderPlatform.CLAUDE_APIKEY;

    protected override IReadOnlyList<IResponseProcessor> GetResponseProcessors(
        UpRequestContext up, DownRequestContext down)
    {
        return
        [

            new ClaudeParseSseResponseProcessor(),
            new UsageAccumulatorResponseProcessor()
        ];
    }

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
            using var response = await SendRequestAsync(up, ct);
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
            new ClaudeMetadataInjectProcessor(Options),
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

        // 优先级 1: metadata.user_id（支持新格式 JSON 和旧格式 legacy string）
        if (root.TryGetPropertyValue("metadata", out var metadataNode) &&
            metadataNode is JsonObject metadata &&
            metadata.TryGetPropertyValue("user_id", out var userIdNode) &&
            userIdNode is JsonValue userIdValue &&
            userIdValue.TryGetValue<string>(out var userIdStr) &&
            !string.IsNullOrWhiteSpace(userIdStr))
        {
            userIdStr = userIdStr.Trim();

            // 新格式: {"device_id":"...","account_uuid":"...","session_id":"..."}
            if (userIdStr.StartsWith('{'))
            {
                try
                {
                    var parsed = JsonNode.Parse(userIdStr);
                    if (parsed is JsonObject jo &&
                        jo.TryGetPropertyValue("session_id", out var sidNode) &&
                        sidNode is JsonValue sidVal &&
                        sidVal.TryGetValue<string>(out var sid) &&
                        !string.IsNullOrWhiteSpace(sid))
                    {
                        down.SessionId = sid;
                        return;
                    }
                }
                catch { /* 解析失败，继续其他格式 */ }
            }

            // 旧格式: user_{64hex}_account_{uuid}_session_{uuid}
            var legacyMatch = System.Text.RegularExpressions.Regex.Match(
                userIdStr,
                @"^user_[a-fA-F0-9]{64}_account_[a-fA-F0-9-]*_session_([a-fA-F0-9\-]{36})$");
            if (legacyMatch.Success)
            {
                down.SessionId = legacyMatch.Groups[1].Value;
                return;
            }

            // 兼容旧逻辑: session_ 前缀
            if (userIdStr.StartsWith("session_", StringComparison.OrdinalIgnoreCase))
            {
                var sessionId = userIdStr["session_".Length..];
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    down.SessionId = sessionId;
                    return;
                }
            }

            // fallback: 直接使用整个字符串
            down.SessionId = userIdStr;
            return;
        }

        // 优先级 2: conversation_id
        if (root.TryGetPropertyValue("conversation_id", out var convIdNode) &&
            convIdNode is JsonValue convIdValue &&
            convIdValue.TryGetValue<string>(out var id) &&
            !string.IsNullOrWhiteSpace(id))
        {
            down.SessionId = id;
            return;
        }

        // 优先级 3: cache_control ephemeral 内容
        var cacheableContent = ExtractCacheableContent(root);
        if (!string.IsNullOrWhiteSpace(cacheableContent))
        {
            down.SessionId = GenerateSessionHashWithContext(cacheableContent, down, apiKeyId);
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
                    down.SessionId = GenerateSessionHashWithContext(text, down, apiKeyId);
                    return;
                }
            }
        }
    }

    public override Task<ModelErrorAnalysisResult> CheckRetryPolicyAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string? responseBody)
    {
        if (statusCode == 400 && ClaudeThinkingCleaner.IsThinkingBlockSignatureError(responseBody))
        {
            Logger.LogWarning("检测到 Claude thinking 签名错误，建议降级重试");
            return Task.FromResult(new ModelErrorAnalysisResult
            {
                IsCanRetry = true,
                RequiresDowngrade = true
            });
        }

        return base.CheckRetryPolicyAsync(statusCode, headers, responseBody);
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

    /// <summary>
    /// 提取带 cache_control: ephemeral 的内容（Prompt Caching 支持）
    /// </summary>
    private static string? ExtractCacheableContent(JsonNode? root)
    {
        if (root is not JsonObject rootObj) return null;

        if (rootObj.TryGetPropertyValue("system", out var systemNode) &&
            systemNode is JsonArray systemArray)
        {
            foreach (var part in systemArray)
            {
                if (part is not JsonObject partObj) continue;
                if (partObj.TryGetPropertyValue("cache_control", out var cacheControlNode) &&
                    cacheControlNode is JsonObject cacheControl &&
                    cacheControl.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue &&
                    typeValue.TryGetValue<string>(out var type) &&
                    type == "ephemeral")
                {
                    if (partObj.TryGetPropertyValue("text", out var textNode) &&
                        textNode is JsonValue textValue &&
                        textValue.TryGetValue<string>(out var text) &&
                        !string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        if (rootObj.TryGetPropertyValue("messages", out var messagesNode) &&
            messagesNode is JsonArray messagesArray)
        {
            foreach (var message in messagesArray)
            {
                if (message is not JsonObject messageObj) continue;
                if (messageObj.TryGetPropertyValue("content", out var contentNode) &&
                    contentNode is JsonArray contentArray)
                {
                    foreach (var part in contentArray)
                    {
                        if (part is not JsonObject partObj) continue;
                        if (partObj.TryGetPropertyValue("cache_control", out var cacheControlNode) &&
                            cacheControlNode is JsonObject cacheControl &&
                            cacheControl.TryGetPropertyValue("type", out var typeNode) &&
                            typeNode is JsonValue typeValue &&
                            typeValue.TryGetValue<string>(out var type) &&
                            type == "ephemeral")
                        {
                            return ExtractTextFromMessageContent(message);
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractTextFromMessageContent(JsonNode? message)
    {
        if (message is not JsonObject messageObj ||
            !messageObj.TryGetPropertyValue("content", out var contentNode))
            return null;

        if (contentNode is JsonValue contentValue &&
            contentValue.TryGetValue<string>(out var contentStr))
            return contentStr;

        if (contentNode is JsonArray contentArray)
        {
            var builder = new StringBuilder();
            foreach (var part in contentArray)
            {
                if (part is not JsonObject partObj) continue;
                if (partObj.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue &&
                    typeValue.TryGetValue<string>(out var type) &&
                    type == "text" &&
                    partObj.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text) &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    builder.Append(text);
                }
            }
            return builder.Length > 0 ? builder.ToString() : null;
        }

        return null;
    }
}

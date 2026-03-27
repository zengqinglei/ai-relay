using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public class OpenAiChatModelHandler(
    ChatModelConnectionOptions options,
    OpenAiCodexInjector openAiCodexInjector,
    IModelProvider modelProvider,
    IHttpClientFactory httpClientFactory,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    ILogger<OpenAiChatModelHandler> logger)
    : BaseChatModelHandler(options, httpClientFactory, streamProcessor, signatureCache, logger)
{
    public override bool Supports(ProviderPlatform platform) =>
        platform is ProviderPlatform.OPENAI_OAUTH or ProviderPlatform.OPENAI_APIKEY;

    public override DownRequestContext CreateDebugDownContext(string modelId, string message)
    {
        var json = new JsonObject
        {
            ["model"] = modelId,
            ["input"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "input_text",
                            ["text"] = message
                        }
                    }
                }
            },
            ["stream"] = true,
            ["instructions"] = "You are a helpful AI assistant."
        };

        if (Options.Platform == ProviderPlatform.OPENAI_OAUTH)
            json["store"] = false;

        string path = Options.Platform == ProviderPlatform.OPENAI_OAUTH
            ? "/backend-api/codex/responses"
            : "/v1/responses";

        return new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = path,
            ModelId = modelId,
            BodyBytes = Encoding.UTF8.GetBytes(json.ToJsonString()).AsMemory()
        };
    }

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return
        [
            new OpenAiUrlProcessor(Options),
            new OpenAiHeaderProcessor(Options),
            new OpenAiModelIdMappingProcessor(modelProvider, Options),
            new OpenAiRequestBodyProcessor(Options, openAiCodexInjector)
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

        // 优先级 1: Header session_id
        if (down.Headers.TryGetValue("session_id", out var sessionIdHeader))
        {
            var sessionId = sessionIdHeader.Trim();
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                down.SessionId = sessionId;
                return;
            }
        }

        // 优先级 2: Header conversation_id
        if (down.Headers.TryGetValue("conversation_id", out var conversationIdHeader))
        {
            var conversationId = conversationIdHeader.Trim();
            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                down.SessionId = conversationId;
                return;
            }
        }

        if (down.BodyJsonNode is not JsonObject root) return;

        // 优先级 3: prompt_cache_key
        if (root.TryGetPropertyValue("prompt_cache_key", out var cacheKeyNode) &&
            cacheKeyNode is JsonValue cacheKeyValue &&
            cacheKeyValue.TryGetValue<string>(out var key) &&
            !string.IsNullOrWhiteSpace(key))
        {
            down.SessionId = key;
            return;
        }

        // 优先级 4: conversation_id in body
        if (root.TryGetPropertyValue("conversation_id", out var convIdNode) &&
            convIdNode is JsonValue convIdValue &&
            convIdValue.TryGetValue<string>(out var id) &&
            !string.IsNullOrWhiteSpace(id))
        {
            down.SessionId = id;
            return;
        }

        // 优先级 5: 第一条消息内容
        if (root.TryGetPropertyValue("messages", out var messagesNode) &&
            messagesNode is JsonArray messages)
        {
            foreach (var messageNode in messages)
            {
                var text = ExtractTextFromMessage(messageNode);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    down.SessionId = GenerateSessionHashWithContext(text, down, apiKeyId);
                    return;
                }
            }
        }
    }

    private static string ExtractTextFromMessage(JsonNode? message)
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
            foreach (var part in contentArray)
            {
                if (part is JsonObject partObj &&
                    partObj.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue &&
                    typeValue.TryGetValue<string>(out var type) &&
                    type == "text" &&
                    partObj.TryGetPropertyValue("text", out var textNode) &&
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

    protected override TimeSpan? ExtractRetryAfter(Dictionary<string, IEnumerable<string>>? headers, string? body)
    {
        // 优先解析 OpenAI 专有 header，再 fallback 到通用解析
        if (headers != null && headers.TryGetValue("x-ratelimit-reset-requests", out var resetValues))
        {
            var resetStr = resetValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(resetStr))
                return ParseOpenAiDuration(resetStr);
        }
        return base.ExtractRetryAfter(headers, body);
    }

    public override ChatResponsePart? ParseChunk(string chunk) =>
        OpenAiChatModelResponseParser.ParseChunkStatic(chunk);

    public override ChatResponsePart ParseCompleteResponse(string responseBody) =>
        OpenAiChatModelResponseParser.ParseCompleteResponseStatic(responseBody);

    private static TimeSpan? ParseOpenAiDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return null;
        duration = duration.Trim().ToLowerInvariant();
        try
        {
            if (duration.EndsWith("ms") && double.TryParse(duration[..^2], out var ms))
                return TimeSpan.FromMilliseconds(ms);
            if (duration.EndsWith("s") && double.TryParse(duration[..^1], out var s))
                return TimeSpan.FromSeconds(s);
            if (duration.EndsWith("m") && double.TryParse(duration[..^1], out var m))
                return TimeSpan.FromMinutes(m);
            if (duration.EndsWith("h") && double.TryParse(duration[..^1], out var h))
                return TimeSpan.FromHours(h);
        }
        catch { }
        return null;
    }
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Common;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

public class OpenAiChatModelHandler(
    ChatModelConnectionOptions options,
    OpenAiCodexInjector openAiCodexInjector,
    IModelProvider modelProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiChatModelHandler> logger)
    : BaseChatModelHandler(options, httpClientFactory, logger)
{
    public override bool Supports(ProviderPlatform platform) =>
        platform is ProviderPlatform.OPENAI_OAUTH or ProviderPlatform.OPENAI_APIKEY;

    protected override IReadOnlyList<IResponseProcessor> GetResponseProcessors(
        UpRequestContext up, DownRequestContext down)
    {
        var processors = new List<IResponseProcessor>
        {
            new OpenAiParseSseResponseProcessor(),
            new UsageAccumulatorResponseProcessor()
        };

        // 下游请求路径为 /chat/completions 时，上游走的是 Responses API（/v1/responses 或 /backend-api/codex/responses）
        // 需要将 Responses API 的 SSE 格式转换为标准 Chat Completions SSE 格式
        if (down.RelativePath.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
            processors.Add(new OpenAiToCompletionResponseProcessor());

        return processors;
    }

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
            RawStream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()))
        };
    }

    protected override IReadOnlyList<IRequestProcessor> GetRequestProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return
        [
            new OpenAiUrlRequestProcessor(Options),
            new OpenAiHeaderRequestProcessor(Options),
            new OpenAiModelIdMappingRequestProcessor(modelProvider, Options),
            new OpenAiModifyBodyRequestProcessor(Options, openAiCodexInjector)
        ];
    }

    public override void ExtractModelInfo(DownRequestContext down, Guid apiKeyId)
    {
        // 提取 ModelId
        if (down.ExtractedProps.TryGetValue("model", out var modelId) && !string.IsNullOrWhiteSpace(modelId))
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

        // 优先级 3: prompt_cache_key
        if (down.ExtractedProps.TryGetValue("prompt_cache_key", out var key) && !string.IsNullOrWhiteSpace(key))
        {
            down.SessionId = key;
            return;
        }

        // 优先级 4: conversation_id in body
        if (down.ExtractedProps.TryGetValue("conversation_id", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            down.SessionId = id;
            return;
        }

        // 优先级 5: 第一条消息内容
        if (down.ExtractedProps.TryGetValue("messages[0].content", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            down.SessionId = GenerateSessionHashWithContext(text, down, apiKeyId);
            return;
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

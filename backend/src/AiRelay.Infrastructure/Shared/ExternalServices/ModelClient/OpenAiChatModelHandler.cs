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
using System.Text.Json;
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
    public override bool Supports(Provider provider, AuthMethod authMethod) =>
        provider == Provider.OpenAI && (authMethod == AuthMethod.OAuth || authMethod == AuthMethod.ApiKey);

    protected override IReadOnlyList<IResponseProcessor> GetResponseProcessors(
        UpRequestContext up, DownRequestContext down)
    {
        return
        [
            new OpenAiParseSseResponseProcessor(),
            new UsageAccumulatorResponseProcessor(),
            new OpenAiToCompletionResponseProcessor(down),
            new OpenAiBufferedChatResponseProcessor(down)
        ];
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

        if (Options.AuthMethod == AuthMethod.OAuth)
            json["store"] = false;
        string path = Options.AuthMethod == AuthMethod.OAuth
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
            new ModelIdMappingRequestProcessor(modelProvider, Options.Provider, Options),
            new OpenAiModifyBodyRequestProcessor(Options, openAiCodexInjector)
        ];
    }

    public override void ExtractModelInfo(DownRequestContext down, Guid apiKeyId)
    {
        // 提取 ModelId
        if (down.ExtractedProps.TryGetValue("public.model", out var modelId) && !string.IsNullOrWhiteSpace(modelId))
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
        if (down.ExtractedProps.TryGetValue("public.conversation_id", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            down.SessionId = id;
            return;
        }

        // 优先级 5: 第一条消息内容（包含智能筛选的 User 内容或保底首条消息）
        if (down.ExtractedProps.TryGetValue("public.fingerprint", out var fingerprint) && !string.IsNullOrWhiteSpace(fingerprint))
        {
            down.SessionId = GenerateSessionHashWithContext(fingerprint, down, apiKeyId);
            return;
        }
    }

    protected override TimeSpan? ExtractRetryAfter(Dictionary<string, IEnumerable<string>>? headers, string? body)
    {
        // 1. OpenAI 专有 header
        if (headers != null && headers.TryGetValue("x-ratelimit-reset-requests", out var resetValues))
        {
            var resetStr = resetValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(resetStr))
                return ParseOpenAiDuration(resetStr);
        }

        // 2. OpenAI body: { "error": { "resets_in_seconds": N } } 或 { "error": { "resets_at": <unix_ts> } }
        if (!string.IsNullOrEmpty(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("resets_in_seconds", out var resetsIn) &&
                        resetsIn.TryGetInt64(out var seconds) && seconds > 0)
                        return TimeSpan.FromSeconds(seconds);

                    if (error.TryGetProperty("resets_at", out var resetsAt) &&
                        resetsAt.TryGetInt64(out var unixTs) && unixTs > 0)
                    {
                        var delta = DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime - DateTime.UtcNow;
                        return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
                    }
                }
            }
            catch { }
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

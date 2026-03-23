using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Parsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Antigravity;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// Antigravity 聊天模型客户端
/// </summary>
public sealed class AntigravityChatModelHandler(
    ChatModelConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    IModelProvider modelProvider,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GoogleSignatureCleaner googleSignatureCleaner,
    AntigravityIdentityInjector antigravityIdentityInjector,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    ILogger<AntigravityChatModelHandler> logger)
    : GoogleInternalChatModelHandlerBase(options, httpClientFactory, streamProcessor, signatureCache, logger)
{
    public override bool Supports(ProviderPlatform platform) =>
        platform == ProviderPlatform.ANTIGRAVITY;

    protected override string? GetFallbackBaseUrl(int statusCode)
    {
        if (statusCode == 429 || statusCode == 408 || statusCode == 404 ||
            (statusCode >= 500 && statusCode < 600))
            return "https://daily-cloudcode-pa.sandbox.googleapis.com";
        return null;
    }

    protected override bool IsChatApiPath(string? path) => true;

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return
        [
            new AntigravityModelIdMappingProcessor(modelProvider),
            new AntigravityUrlProcessor(_options),
            new AntigravityHeaderProcessor(_options),
            new AntigravityRequestBodyProcessor(
                _options, antigravityIdentityInjector,
                googleJsonSchemaCleaner, logger),
            new AntigravityDegradationProcessor(degradationLevel, googleSignatureCleaner, logger),
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

        if (down.BodyJsonNode is not JsonObject root) return;

        // 优先级 1: conversation_id
        if (root.TryGetPropertyValue("conversation_id", out var convIdNode) &&
            convIdNode is JsonValue convIdValue &&
            convIdValue.TryGetValue<string>(out var id) &&
            !string.IsNullOrWhiteSpace(id))
        {
            down.SessionHash = id;
            return;
        }

        // 优先级 2: 第一条消息内容
        if (root.TryGetPropertyValue("contents", out var contentsNode) &&
            contentsNode is JsonArray contents)
        {
            foreach (var contentNode in contents)
            {
                var text = GeminiTextExtractor.ExtractTextFromParts(contentNode);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    down.SessionHash = GenerateSessionHashWithContext(text, down, apiKeyId);
                    return;
                }
            }
        }
    }

    public override async Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody)
    {
        if (statusCode == 400)
        {
            if (GoogleSignatureCleaner.IsSignatureError(responseBody))
            {
                return new ModelErrorAnalysisResult
                {
                    ErrorType = ModelErrorType.SignatureError,
                    IsRetryableOnSameAccount = true,
                    RequiresDowngrade = true,
                    RetryAfter = null
                };
            }
            return new ModelErrorAnalysisResult
            {
                ErrorType = ModelErrorType.BadRequest,
                IsRetryableOnSameAccount = false,
                RequiresDowngrade = false,
                RetryAfter = null
            };
        }

        return await base.AnalyzeErrorAsync(statusCode, headers, responseBody);
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

    public override ChatResponsePart? ParseChunk(string chunk) =>
        GeminiChatModelResponseParser.ParseChunkStatic(chunk);

    public override ChatResponsePart ParseCompleteResponse(string responseBody) =>
        GeminiChatModelResponseParser.ParseCompleteResponseStatic(responseBody);
}

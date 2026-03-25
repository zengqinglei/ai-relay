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
using System.Text.Json;

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

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return
        [
            new AntigravityModelIdMappingProcessor(modelProvider, Options),
            new AntigravityUrlProcessor(Options),
            new AntigravityHeaderProcessor(Options),
            new AntigravityRequestBodyProcessor(
                Options, antigravityIdentityInjector,
                googleJsonSchemaCleaner, Logger),
            new AntigravityDegradationProcessor(degradationLevel, googleSignatureCleaner, Logger),
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
            down.SessionId = id;
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
                    down.SessionId = GenerateSessionHashWithContext(text, down, apiKeyId);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 拉取可用模型配额信息
    /// </summary>
    public override async Task<IReadOnlyList<AccountQuotaInfo>?> FetchQuotaAsync(CancellationToken ct = default)
    {
        var projectId = Options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
        var body = JsonSerializer.Serialize(new { project = projectId });
        var down = new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = "/v1internal:fetchAvailableModels",
            BodyBytes = Encoding.UTF8.GetBytes(body).AsMemory()
        };
        var up = await ProcessRequestContextAsync(down, 0, ct);
        using var response = await ProxyRequestAsync(up, ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogDebug("Antigravity 配额拉取失败: {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("models", out var models))
            return null;

        var quotaList = new List<AccountQuotaInfo>();

        foreach (var model in models.EnumerateObject())
        {
            var modelId = model.Name;

            double? remainingFraction = null;
            string? resetTime = null;

            if (model.Value.TryGetProperty("quotaInfo", out var quotaInfo))
            {
                if (quotaInfo.TryGetProperty("remainingFraction", out var fraction))
                    remainingFraction = fraction.GetDouble();
                if (quotaInfo.TryGetProperty("resetTime", out var reset))
                    resetTime = reset.GetString();
            }

            quotaList.Add(new AccountQuotaInfo
            {
                ModelId = modelId,
                RemainingQuota = remainingFraction.HasValue ? (int)(remainingFraction.Value * 100) : null,
                QuotaResetTime = resetTime,
                LastRefreshed = DateTime.UtcNow
            });
        }

        return quotaList.Count > 0 ? quotaList : null;
    }

    public override async Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default)
    {
        var quotaList = await FetchQuotaAsync(ct);
        if (quotaList == null || quotaList.Count == 0)
            return null;

        var upstreamModels = quotaList
            .Select(q => q.ModelId)
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Logger.LogInformation("Antigravity 上游拉取成功: {Count} 个模型", upstreamModels.Count);

        // 返回上游模型列表（后续在 AppService 中与静态列表交集）
        return upstreamModels.Select(m => new ModelOption(m!, m!)).ToList();
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

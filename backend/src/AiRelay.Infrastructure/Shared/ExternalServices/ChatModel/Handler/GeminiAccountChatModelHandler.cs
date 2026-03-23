using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Parsing;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Gemini;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public class GeminiAccountChatModelHandler(
    ChatModelConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GoogleSignatureCleaner googleSignatureCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    ILogger<GeminiAccountChatModelHandler> logger)
    : GoogleInternalChatModelHandlerBase(options, httpClientFactory, streamProcessor, signatureCache, logger)
{
    // Gemini CLI 临时目录正则匹配: .gemini/tmp/[64位哈希]
    private static readonly Regex GeminiCliTmpDirRegex = new(@"\.gemini/tmp/([A-Fa-f0-9]{64})", RegexOptions.Compiled);

    public override bool Supports(ProviderPlatform platform) =>
        platform == ProviderPlatform.GEMINI_OAUTH;

    protected override string? GetFallbackBaseUrl(int statusCode)
    {
        if (statusCode == 429 || statusCode == 408 || statusCode == 404 ||
            (statusCode >= 500 && statusCode < 600))
            return "https://daily-cloudcode-pa.sandbox.googleapis.com";
        return null;
    }

    protected override bool IsChatApiPath(string? path) =>
        path != null && (
            path.EndsWith(":streamGenerateContent", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(":generateContent", StringComparison.OrdinalIgnoreCase));

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        var isChatApi = IsChatApiPath(down.RelativePath);
        var processors = new List<IRequestProcessor> {
            new GeminiOAuthUrlProcessor(isChatApi, _options),
            new GeminiHeaderProcessor(_options)
        };
        if (isChatApi)
        {
            processors.AddRange(
                new GeminiOAuthRequestBodyProcessor(_options, googleJsonSchemaCleaner, geminiSystemPromptInjector),
                new GeminiDegradationProcessor(degradationLevel, googleSignatureCleaner, logger));
        }
        return processors;
    }

    public override void ExtractModelInfo(DownRequestContext down, Guid apiKeyId)
    {
        // 1. 提取 ModelId — 优先从 URL 路径提取
        if (!string.IsNullOrEmpty(down.RelativePath) && down.RelativePath.Contains("/models/"))
        {
            var parts = down.RelativePath.Split(["/models/"], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var potentialModel = parts.Last();
                var colonIndex = potentialModel.IndexOf(':');
                if (colonIndex > 0)
                    down.ModelId = potentialModel[..colonIndex];
                else
                {
                    var slashIndex = potentialModel.IndexOf('/');
                    down.ModelId = slashIndex > 0 ? potentialModel[..slashIndex] : potentialModel;
                }
            }
        }

        // 2. 从 Body 提取
        if (string.IsNullOrEmpty(down.ModelId) &&
            down.BodyJsonNode is JsonObject obj &&
            obj.TryGetPropertyValue("model", out var modelProp) &&
            modelProp is JsonValue modelValue &&
            modelValue.TryGetValue<string>(out var modelId))
        {
            down.ModelId = modelId;
        }

        // ========== 提取 SessionHash ==========
        // 优先级 1: Gemini CLI 专用逻辑 (从 tmp 目录提取)
        var match = down.SearchBodyPattern(GeminiCliTmpDirRegex, maxSearchLength: 50000);
        if (match.Success && match.Groups.Count >= 2)
        {
            var tmpDirHash = match.Groups[1].Value;

            string? sessionId = null;
            if (down.BodyJsonNode is JsonObject body)
            {
                var payload = body.ContainsKey("request") ? body["request"] as JsonObject : body;
                if (payload != null &&
                    payload.TryGetPropertyValue("session_id", out var sessionIdNode) &&
                    sessionIdNode is JsonValue sessionIdValue)
                    sessionIdValue.TryGetValue<string>(out sessionId);
            }

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var combined = $"{sessionId.Trim()}:{tmpDirHash}";
                var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
                down.SessionHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            else
            {
                down.SessionHash = tmpDirHash;
            }
            return;
        }

        if (down.BodyJsonNode is JsonObject root)
        {
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
    }

    public override async Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody)
    {
        if (statusCode == 400 && GoogleSignatureCleaner.IsSignatureError(responseBody))
        {
            return new ModelErrorAnalysisResult
            {
                ErrorType = ModelErrorType.SignatureError,
                IsRetryableOnSameAccount = true,
                RequiresDowngrade = true,
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
            RelativePath = $"/v1internal:streamGenerateContent",
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

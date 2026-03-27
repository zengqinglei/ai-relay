using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Parsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Gemini;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public class GeminiApiChatModelHandler(
    ChatModelConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GoogleSignatureCleaner googleSignatureCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    ILogger<GeminiApiChatModelHandler> logger)
    : BaseChatModelHandler(options, httpClientFactory, streamProcessor, signatureCache, logger)
{
    // Gemini CLI 临时目录正则匹配: .gemini/tmp/[64位哈希]
    private static readonly Regex GeminiCliTmpDirRegex = new(@"\.gemini/tmp/([A-Fa-f0-9]{64})", RegexOptions.Compiled);

    public override bool Supports(ProviderPlatform platform) =>
        platform == ProviderPlatform.GEMINI_APIKEY;

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return [
            new GeminiApiKeyUrlProcessor(Options),
            new GeminiHeaderProcessor(Options),
            new GeminiApiKeyRequestBodyProcessor(
                googleJsonSchemaCleaner,
                geminiSystemPromptInjector,
                Options.ShouldMimicOfficialClient),
            new GeminiDegradationProcessor(degradationLevel, googleSignatureCleaner, logger)
        ];
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

            string? privilegedUserId = null;
            if (down.Headers.TryGetValue("x-gemini-api-privileged-user-id", out var headerVal))
                privilegedUserId = headerVal;

            if (!string.IsNullOrWhiteSpace(privilegedUserId))
            {
                var combined = $"{privilegedUserId.Trim()}:{tmpDirHash}";
                var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
                down.SessionId = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            else
            {
                down.SessionId = tmpDirHash;
            }
            return;
        }

        if (down.BodyJsonNode is JsonObject root)
        {
            // 优先级 2: 只取第一条消息内容
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
    }

    public override async Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default)
    {
        try
        {
            // Gemini API Key 通过 URL 参数传递，Processor 会自动处理
            var down = new DownRequestContext
            {
                Method = HttpMethod.Get,
                RelativePath = "/v1beta/models",
                Headers = []
            };

            var up = await ProcessRequestContextAsync(down, 0, ct);
            using var response = await ProxyRequestAsync(up, ct);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Gemini 上游模型拉取失败: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var models = new List<ModelOption>();

            if (doc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var item in modelsArray.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameProp))
                    {
                        var fullName = nameProp.GetString(); // "models/gemini-2.5-pro"
                        if (!string.IsNullOrEmpty(fullName) && fullName.StartsWith("models/"))
                        {
                            var modelId = fullName.Substring(7);

                            // 过滤：仅保留 generateContent 支持的模型
                            if (item.TryGetProperty("supportedGenerationMethods", out var methodsArray))
                            {
                                var methods = methodsArray.EnumerateArray()
                                    .Select(m => m.GetString())
                                    .Where(m => m != null)
                                    .ToList();

                                if (methods.Contains("generateContent"))
                                {
                                    var displayName = item.TryGetProperty("displayName", out var dispProp)
                                        ? dispProp.GetString() ?? modelId
                                        : modelId;
                                    models.Add(new ModelOption(displayName, modelId));
                                }
                            }
                        }
                    }
                }
            }

            logger.LogInformation("Gemini 上游拉取成功: {Count} 个模型", models.Count);
            return models.Count > 0 ? models : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gemini 上游模型拉取异常");
            return null;
        }
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

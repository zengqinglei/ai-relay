using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Parsing;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Gemini;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public class GeminiAccountChatModelHandler(
    ChatModelConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GoogleSignatureCleaner googleSignatureCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector,
    ISignatureCache signatureCache,
    ILogger<GeminiAccountChatModelHandler> logger)
    : GoogleInternalChatModelHandlerBase(options, httpClientFactory, signatureCache, logger)
{
    // Gemini CLI 临时目录正则匹配: .gemini/tmp/[64位哈希]
    private static readonly Regex GeminiCliTmpDirRegex = new(@"\.gemini/tmp/([A-Fa-f0-9]{64})", RegexOptions.Compiled);

    public override bool Supports(ProviderPlatform platform) =>
        platform == ProviderPlatform.GEMINI_OAUTH;

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return [
            new GeminiOAuthUrlProcessor(Options),
            new GeminiHeaderProcessor(Options),
            new GeminiOAuthRequestBodyProcessor(Options, googleJsonSchemaCleaner, geminiSystemPromptInjector),
            new GeminiDegradationProcessor(degradationLevel, googleSignatureCleaner, logger)
        ];
    }

    public override void ExtractModelInfo(DownRequestContext down, Guid apiKeyId)
    {
        down.PromptIndex = ExtractPromptIndex(down.BodyJsonNode);

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
    }

    /// <summary>
    /// 拉取用户配额信息（包含可用模型列表）
    /// </summary>
    public override async Task<IReadOnlyList<AccountQuotaInfo>?> FetchQuotaAsync(CancellationToken ct = default)
    {
        var projectId = Options.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : "";
        var body = JsonSerializer.Serialize(new { project = projectId });
        var down = new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = "/v1internal:retrieveUserQuota",
            BodyBytes = Encoding.UTF8.GetBytes(body).AsMemory()
        };
        var up = await ProcessRequestContextAsync(down, 0, ct);
        using var response = await SendRequestAsync(up, ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogDebug("Gemini OAuth 配额拉取失败: {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("buckets", out var buckets))
            return null;

        var quotaBuckets = new List<AccountQuotaInfo>();

        foreach (var bucket in buckets.EnumerateArray())
        {
            var modelIdStr = bucket.TryGetProperty("modelId", out var modelIdProp)
                ? modelIdProp.GetString()
                : null;

            var resetTime = bucket.TryGetProperty("resetTime", out var resetProp)
                ? resetProp.GetString()
                : null;

            var tokenType = bucket.TryGetProperty("tokenType", out var tokenTypeProp)
                ? tokenTypeProp.GetString()
                : null;

            var remainingFraction = bucket.TryGetProperty("remainingFraction", out var fractionProp)
                ? fractionProp.GetDecimal()
                : 0m;

            if (!string.IsNullOrEmpty(modelIdStr))
            {
                quotaBuckets.Add(new AccountQuotaInfo
                {
                    ModelId = modelIdStr,
                    QuotaResetTime = resetTime ?? string.Empty
                });
            }
        }

        return quotaBuckets;
    }

    public override async Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default)
    {
        var buckets = await FetchQuotaAsync(ct);
        if (buckets == null || buckets.Count == 0)
            return null;

        var upstreamModels = buckets
            .Select(b => b.ModelId)
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Logger.LogInformation("Gemini OAuth 上游拉取成功: {Count} 个模型", upstreamModels.Count);

        return upstreamModels.Select(m => new ModelOption(m!, m!)).ToList();
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
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.SignatureCache;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

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
    public override bool Supports(Provider provider, AuthMethod authMethod) =>
        provider == Provider.Gemini && authMethod == AuthMethod.OAuth;

    protected override IReadOnlyList<IRequestProcessor> GetRequestProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return [
            new GeminiOAuthUrlRequestProcessor(Options),
            new GeminiHeaderRequestProcessor(Options),
            new GeminiOAuthModifyBodyRequestProcessor(Options, googleJsonSchemaCleaner, geminiSystemPromptInjector, SignatureCache),
            new GeminiDegradationRequestProcessor(degradationLevel, googleSignatureCleaner)
        ];
    }

    public override void ExtractModelInfo(DownRequestContext down, Guid apiKeyId)
    {
        // 提取 PromptIndex：统计 contents[]/messages[] 中 user 角色数量 - 1
        if (down.ExtractedProps.TryGetValue("user_role_count", out var countStr) &&
            int.TryParse(countStr, out var count) && count > 0)
        {
            down.PromptIndex = count - 1;
        }
        else
        {
            down.PromptIndex = 0;
        }

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
            down.ExtractedProps.TryGetValue("model", out var modelId) &&
            !string.IsNullOrWhiteSpace(modelId))
        {
            down.ModelId = modelId;
        }

        // ========== 提取 SessionHash ==========
        // 优先级 1: Gemini CLI 专用逻辑 (从 tmp 目录提取)
        if (down.ExtractedProps.TryGetValue("gemini_cli_tmp_hash", out var tmpDirHash) && !string.IsNullOrWhiteSpace(tmpDirHash))
        {
            down.ExtractedProps.TryGetValue("request.session_id", out var sessionId);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                down.ExtractedProps.TryGetValue("session_id", out sessionId);
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

        // 优先级 2: 第一条消息内容
        if (down.ExtractedProps.TryGetValue("session_fingerprint_text", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            down.SessionId = GenerateSessionHashWithContext(text, down, apiKeyId);
            return;
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
            RawStream = new MemoryStream(Encoding.UTF8.GetBytes(body))
        };
        var up = await ProcessRequestContextAsync(down, 0, ct);
        using var response = await SendCoreRequestAsync(up, down, ct);

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
            RawStream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()))
        };
    }
}

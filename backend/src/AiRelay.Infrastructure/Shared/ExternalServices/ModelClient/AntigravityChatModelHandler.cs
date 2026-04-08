using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Antigravity;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

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
    ISignatureCache signatureCache,
    ILogger<AntigravityChatModelHandler> logger)
    : GoogleInternalChatModelHandlerBase(options, httpClientFactory, signatureCache, logger)
{
    public override bool Supports(ProviderPlatform platform) =>
        platform == ProviderPlatform.ANTIGRAVITY;

    protected override IReadOnlyList<IRequestProcessor> GetRequestProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return
        [
            new AntigravityModelIdMappingRequestProcessor(modelProvider, Options),
            new AntigravityUrlRequestProcessor(Options),
            new AntigravityHeaderRequestProcessor(Options),
            new AntigravityModifyBodyRequestProcessor(
                Options, antigravityIdentityInjector,
                googleJsonSchemaCleaner, Logger),
            new AntigravityDegradationRequestProcessor(degradationLevel, googleSignatureCleaner, Logger),
        ];
    }

    public override void ExtractModelInfo(DownRequestContext down, Guid apiKeyId)
    {
        // 提取 ModelId
        if (down.ExtractedProps.TryGetValue("model", out var modelId) && !string.IsNullOrWhiteSpace(modelId))
        {
            down.ModelId = modelId;
        }

        // 优先级 1: conversation_id
        if (down.ExtractedProps.TryGetValue("conversation_id", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            down.SessionId = id;
            return;
        }

        // 优先级 2: 第一条消息内容
        if (down.ExtractedProps.TryGetValue("messages[0].content", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            down.SessionId = GenerateSessionHashWithContext(text, down, apiKeyId);
            return;
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
            RawStream = new MemoryStream(Encoding.UTF8.GetBytes(body))
        };
        var up = await ProcessRequestContextAsync(down, 0, ct);
        using var response = await SendCoreRequestAsync(up, down, ct);

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
            var modelIdStr = model.Name;

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
                ModelId = modelIdStr,
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
            RelativePath = $"/v1beta/models/{modelId}:streamGenerateContent",
            QueryString = "?alt=sse",
            ModelId = modelId,
            RawStream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()))
        };
    }
}

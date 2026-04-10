using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

public class GeminiApiChatModelHandler(
    ChatModelConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    GoogleJsonSchemaCleaner googleJsonSchemaCleaner,
    GoogleSignatureCleaner googleSignatureCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector,
    ISignatureCache signatureCache,
    ILogger<GeminiApiChatModelHandler> logger)
    : GoogleInternalChatModelHandlerBase(options, httpClientFactory, signatureCache, logger)
{
    public override bool Supports(Provider provider, AuthMethod authMethod) =>
        provider == Provider.Gemini && authMethod == AuthMethod.ApiKey;

    protected override IReadOnlyList<IRequestProcessor> GetRequestProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return [
            new GeminiApiKeyUrlRequestProcessor(Options),
            new GeminiHeaderRequestProcessor(Options),
            new GeminiApiKeyModifyBodyRequestProcessor(
                googleJsonSchemaCleaner,
                geminiSystemPromptInjector,
                Options.ShouldMimicOfficialClient),
            new GeminiDegradationRequestProcessor(degradationLevel, googleSignatureCleaner, Logger)
        ];
    }

    protected override string? GetFallbackBaseUrl(int statusCode) => null;
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
            down.ExtractedProps.TryGetValue("model", out var modelId) &&
            !string.IsNullOrWhiteSpace(modelId))
        {
            down.ModelId = modelId;
        }

        // ========== 提取 SessionHash ==========
        // 优先级 1: Gemini CLI 专用逻辑 (从 tmp 目录提取)
        // ApiKey 场景：通过 header x-gemini-api-privileged-user-id + tmpDirHash 组合
        if (down.ExtractedProps.TryGetValue("gemini_cli_tmp_hash", out var tmpDirHash) && !string.IsNullOrWhiteSpace(tmpDirHash))
        {
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

        // 优先级 2: 第一条消息内容
        if (down.ExtractedProps.TryGetValue("session_fingerprint_text", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            down.SessionId = GenerateSessionHashWithContext(text, down, apiKeyId);
            return;
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
            using var response = await SendCoreRequestAsync(up, down, ct);

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
                            var modelIdStr = fullName[7..];

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
                                        ? dispProp.GetString() ?? modelIdStr
                                        : modelIdStr;
                                    models.Add(new ModelOption(displayName, modelIdStr));
                                }
                            }
                        }
                    }
                }
            }

            Logger.LogInformation("Gemini 上游拉取成功: {Count} 个模型", models.Count);
            return models.Count > 0 ? models : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Gemini 上游模型拉取异常");
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
            RawStream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()))
        };
    }

    public override Task<ModelErrorAnalysisResult> CheckRetryPolicyAsync(int statusCode, Dictionary<string, IEnumerable<string>>? headers, string? responseBody) =>
        base.CheckRetryPolicyAsync(statusCode, headers, responseBody);

    public override Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default) =>
        Task.FromResult(new ConnectionValidationResult(true));
}

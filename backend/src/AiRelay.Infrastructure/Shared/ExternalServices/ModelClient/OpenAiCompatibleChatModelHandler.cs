using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAiCompatible;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Common;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

/// <summary>
/// OpenAI Compatible 聊天模型处理器
/// </summary>
public class OpenAiCompatibleChatModelHandler(
    ChatModelConnectionOptions options,
    IModelProvider modelProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiCompatibleChatModelHandler> logger)
    : BaseChatModelHandler(options, httpClientFactory, logger)
{
    public override bool Supports(Provider provider, AuthMethod authMethod) =>
        provider == Provider.OpenAICompatible && authMethod == AuthMethod.ApiKey;

    protected override IReadOnlyList<IRequestProcessor> GetRequestProcessors(DownRequestContext down, int degradationLevel)
    {
        return
        [
            new OpenAiCompatibleUrlRequestProcessor(Options),
            new OpenAiCompatibleHeaderRequestProcessor(Options),
            new ModelIdMappingRequestProcessor(modelProvider, Options.Provider, Options),
            new OpenAiCompatibleModifyBodyRequestProcessor(Options)
        ];
    }

    protected override IReadOnlyList<IResponseProcessor> GetResponseProcessors(UpRequestContext up, DownRequestContext down)
    {
        return
        [
            // 复用 OpenAI 的 SSE 解析逻辑，因为协议一致
            new OpenAiParseSseResponseProcessor(),
            new UsageAccumulatorResponseProcessor()
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

        // 优先级 2: 第一条消息内容
        if (down.ExtractedProps.TryGetValue("public.fingerprint", out var fingerprint) && !string.IsNullOrWhiteSpace(fingerprint))
        {
            down.SessionId = GenerateSessionHashWithContext(fingerprint, down, apiKeyId);
            return;
        }
    }

    public override async Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default)
    {
        var down = new DownRequestContext
        {
            Method = HttpMethod.Get,
            RelativePath = "/v1/models"
        };

        var up = await ProcessRequestContextAsync(down, 0, ct);
        using var response = await SendCoreRequestAsync(up, down, ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("OpenAICompatible 上游模型拉取失败: {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var models = new List<ModelOption>();

        if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataArray.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idProp))
                {
                    var modelId = idProp.GetString();
                    if (!string.IsNullOrEmpty(modelId))
                    {
                        // 暂时不硬编码 displayName，直接使用 modelId
                        models.Add(new ModelOption(modelId, modelId));
                    }
                }
            }
        }

        Logger.LogInformation("OpenAICompatible 上游拉取成功: {Count} 个模型", models.Count);
        return models.Count > 0 ? models : null;
    }

    public override Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new ConnectionValidationResult(true));
    }

    public override DownRequestContext CreateDebugDownContext(string modelId, string message)
    {
        var json = new JsonObject
        {
            ["model"] = modelId,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = message
                }
            },
            ["stream"] = true
        };

        return new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = "/v1/chat/completions",
            ModelId = modelId,
            RawStream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()))
        };
    }
}

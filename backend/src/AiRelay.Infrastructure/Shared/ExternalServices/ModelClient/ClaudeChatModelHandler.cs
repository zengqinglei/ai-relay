using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Common;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Claude;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

public class ClaudeChatModelHandler(
    ChatModelConnectionOptions options,
    ClaudeRequestCleaner claudeRequestCleaner,
    ClaudeThinkingCleaner claudeThinkingCleaner,
    ClaudeCacheControlCleaner claudeCacheControlCleaner,
    ClaudeSystemPromptInjector claudeSystemPromptInjector,
    IModelProvider modelProvider,
    IClaudeCodeClientDetector clientDetector,
    IHttpClientFactory httpClientFactory,
    ILogger<ClaudeChatModelHandler> logger)
    : BaseChatModelHandler(options, httpClientFactory, logger)
{
    public override bool Supports(Provider provider, AuthMethod authMethod) =>
        provider == Provider.Claude && (authMethod == AuthMethod.OAuth || authMethod == AuthMethod.ApiKey);

    protected override IReadOnlyList<IResponseProcessor> GetResponseProcessors(
        UpRequestContext up, DownRequestContext down)
    {
        return
        [
            new ClaudeParseSseResponseProcessor(),
            new UsageAccumulatorResponseProcessor()
        ];
    }

    public override async Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default)
    {
        // 仅 ApiKey 支持
        if (Options.AuthMethod != AuthMethod.ApiKey)
            return null;

        // 1. 构造 DownRequestContext（GET /v1/models）
        var down = new DownRequestContext
        {
            Method = HttpMethod.Get,
            RelativePath = "/v1/models",
            Headers = []
        };

        // 2. 通过 Processor 链处理（复用 Header 处理逻辑）
        var up = await ProcessRequestContextAsync(down, 0, ct);

        // 3. 发送请求
        using var response = await SendCoreRequestAsync(up, down, ct);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("Claude 上游模型拉取失败: {StatusCode}", response.StatusCode);
            return null;
        }

        // 4. 解析响应（自动解压已由 ModelProxyClient 拦截）
        await using var responseStream = await response.Content.ReadAsStreamAsync(ct);

        using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct);
        var models = new List<ModelOption>();

        if (doc.RootElement.TryGetProperty("data", out var dataArray))
        {
            foreach (var item in dataArray.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idProp))
                {
                    var id = idProp.GetString();
                    if (!string.IsNullOrEmpty(id) && id.StartsWith("claude-"))
                    {
                        var displayName = item.TryGetProperty("display_name", out var nameProp)
                            ? nameProp.GetString() ?? id
                            : id;
                        models.Add(new ModelOption(displayName, id));
                    }
                }
            }
        }

        Logger.LogInformation("Claude 上游拉取成功: {Count} 个模型", models.Count);
        return models.Count > 0 ? models : null;
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
            ["max_tokens"] = 1024,
            ["stream"] = true
        };

        return new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = "/v1/messages",
            ModelId = modelId,
            RawStream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString())),
            Headers = []
        };
    }

    protected override IReadOnlyList<IRequestProcessor> GetRequestProcessors(
        DownRequestContext down, int degradationLevel)
    {
        return [
            new ModelIdMappingRequestProcessor(modelProvider, Options.Provider, Options),
            new ClaudeUrlRequestProcessor(Options),
            new ClaudeHeaderRequestProcessor(Options, clientDetector),
            new ClaudeModifyBodyRequestProcessor(
                Options,
                claudeRequestCleaner,
                claudeCacheControlCleaner,
                claudeSystemPromptInjector,
                clientDetector),
            new ClaudeDegradationRequestProcessor(degradationLevel, claudeThinkingCleaner, Logger)
        ];
    }

    public override void ExtractModelInfo(DownRequestContext down, Guid apiKeyId)
    {
        // 提取 ModelId
        if (down.ExtractedProps.TryGetValue("public.model", out var modelId) && !string.IsNullOrWhiteSpace(modelId))
        {
            down.ModelId = modelId;
        }

        // 提取 SessionHash
        // 优先级 1: claude.metadata_user_id（支持新格式 JSON 和旧格式 legacy string）
        if (down.ExtractedProps.TryGetValue("claude.metadata_user_id", out var userIdStr) && !string.IsNullOrWhiteSpace(userIdStr))
        {
            userIdStr = userIdStr.Trim();

            // 新格式: {"device_id":"...","account_uuid":"...","session_id":"..."}
            if (userIdStr.StartsWith('{'))
            {
                try
                {
                    var parsed = JsonNode.Parse(userIdStr);
                    if (parsed is JsonObject jo &&
                        jo.TryGetPropertyValue("session_id", out var sidNode) &&
                        sidNode is JsonValue sidVal &&
                        sidVal.TryGetValue<string>(out var sid) &&
                        !string.IsNullOrWhiteSpace(sid))
                    {
                        down.SessionId = sid;
                        return;
                    }
                }
                catch { /* 解析失败，继续其他格式 */ }
            }

            // 旧格式: user_{64hex}_account_{uuid}_session_{uuid}
            var legacyMatch = System.Text.RegularExpressions.Regex.Match(
                userIdStr,
                @"^user_[a-fA-F0-9]{64}_account_[a-fA-F0-9-]*_session_([a-fA-F0-9\-]{36})$");
            if (legacyMatch.Success)
            {
                down.SessionId = legacyMatch.Groups[1].Value;
                return;
            }

            // 兼容旧逻辑: session_ 前缀
            if (userIdStr.StartsWith("session_", StringComparison.OrdinalIgnoreCase))
            {
                var sessionId = userIdStr["session_".Length..];
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    down.SessionId = sessionId;
                    return;
                }
            }

            // fallback: 直接使用整个字符串
            down.SessionId = userIdStr;
            return;
        }

        // 优先级 2: conversation_id
        if (down.ExtractedProps.TryGetValue("public.conversation_id", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            down.SessionId = id;
            return;
        }

        // 优先级 3: system[] 中带 cache_control ephemeral 的文本内容
        if (down.ExtractedProps.TryGetValue("claude.cache_text", out var cacheText) && !string.IsNullOrWhiteSpace(cacheText))
        {
            down.SessionId = GenerateSessionHashWithContext(cacheText, down, apiKeyId);
            return;
        }

        // 优先级 4: 第一条消息内容
        if (down.ExtractedProps.TryGetValue("public.fingerprint", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            down.SessionId = GenerateSessionHashWithContext(text, down, apiKeyId);
            return;
        }
    }

    public override Task<ModelErrorAnalysisResult> CheckRetryPolicyAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string? responseBody)
    {
        if (statusCode == 400 && ClaudeThinkingCleaner.IsThinkingBlockSignatureError(responseBody))
        {
            Logger.LogWarning("检测到 Claude thinking 签名错误，建议降级重试");
            return Task.FromResult(new ModelErrorAnalysisResult
            {
                IsCanRetry = true,
                RequiresDowngrade = true
            });
        }

        return base.CheckRetryPolicyAsync(statusCode, headers, responseBody);
    }

}

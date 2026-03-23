using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public abstract class BaseChatModelHandler : IChatModelHandler
{
    private const int SessionHashLength = 16;
    private const int FallbackSessionIdLength = 19;
    protected const int BackoffBaseSeconds = 5;
    protected const int BackoffMaxSeconds = 3600;

    protected readonly ChatModelConnectionOptions _options;
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly SseResponseStreamProcessor StreamProcessor;
    protected readonly ISignatureCache SignatureCache;
    protected readonly ILogger Logger;

    protected BaseChatModelHandler(
        ChatModelConnectionOptions options,
        IHttpClientFactory httpClientFactory,
        SseResponseStreamProcessor streamProcessor,
        ISignatureCache signatureCache,
        ILogger logger)
    {
        _options = options;
        HttpClientFactory = httpClientFactory;
        StreamProcessor = streamProcessor;
        SignatureCache = signatureCache;
        Logger = logger;
    }

    public abstract bool Supports(ProviderPlatform platform);

    protected virtual string? GetFallbackBaseUrl(int statusCode) => null;

    // ── Processor 组合（子类实现，路由感知逻辑集中于此）

    /// <summary>
    /// 判断当前路径是否为聊天 API 路径。
    /// 各平台显式声明自己的聊天端点，非聊天路径（管理 API 等）仅走 Header 认证链。
    /// </summary>
    protected abstract bool IsChatApiPath(string? path);

    /// <summary>
    /// 子类根据请求路由（down.RelativePath）和降级级别返回 Processor 列表
    /// </summary>
    protected abstract IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down,
        int degradationLevel);

    // ── ProcessRequestContextAsync

    public async Task<UpRequestContext> ProcessRequestContextAsync(
        DownRequestContext down,
        int degradationLevel = 0,
        CancellationToken ct = default)
    {
        var up = new UpRequestContext { Method = down.Method, MappedModelId = down.ModelId };

        foreach (var processor in GetProcessors(down, degradationLevel))
        {
            await processor.ProcessAsync(down, up, ct);
        }

        return up;
    }

    // ── ProxyRequestAsync（含 Fallback 重试）

    public async Task<HttpResponseMessage> ProxyRequestAsync(
        UpRequestContext up,
        CancellationToken ct = default)
    {
        var currentBaseUrl = up.BaseUrl;
        var hasTriedFallback = false;

        while (true)
        {
            try
            {
                var response = await ExecuteHttpAsync(up, currentBaseUrl, ct);

                if (response.IsSuccessStatusCode)
                    return response;

                var fallbackUrl = GetFallbackBaseUrl((int)response.StatusCode);
                if (fallbackUrl != null && !hasTriedFallback)
                {
                    Logger.LogWarning("端点异常 ({StatusCode})，切换备用端点", response.StatusCode);
                    response.Dispose();
                    hasTriedFallback = true;
                    currentBaseUrl = fallbackUrl;
                    continue;
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "HTTP 请求失败");
                throw;
            }
        }
    }

    protected virtual async Task<HttpResponseMessage> ExecuteHttpAsync(
        UpRequestContext up,
        string baseUrl,
        CancellationToken ct = default)
    {
        var httpClient = HttpClientFactory.CreateClient();

        var normalizedBase = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
        var relativeUrl = up.RelativePath.TrimStart('/') + (up.QueryString ?? "");

        var request = new HttpRequestMessage(up.Method, normalizedBase + relativeUrl);

        foreach (var header in up.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (up.BodyJson != null)
        {
            var bodyContent = up.BodyJson.ToJsonString();
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(bodyContent));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    // ── SSE Line Callback

    public virtual Action<string>? GetSseLineCallback(string? sessionId) => null;

    // ── Error Analysis

    public virtual Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody)
    {
        var result = new ModelErrorAnalysisResult
        {
            ErrorType = ModelErrorType.Unknown,
            IsRetryableOnSameAccount = false,
            RequiresDowngrade = false,
            RetryAfter = null
        };

        // 默认判断逻辑：5xx 视为 ServerError
        if (statusCode >= 500 && statusCode < 600)
        {
            result.ErrorType = ModelErrorType.ServerError;
            result.IsRetryableOnSameAccount = true; // 临时故障，可尝试同账号重试

            // 🟢 统一退避策略：优先遵循官方 Retry-After，否则使用斐波那契退避
            result.RetryAfter = ExtractRetryAfterGeneric(headers, responseBody);
        }
        // 429 视为 RateLimit
        else if (statusCode == 429)
        {
            result.ErrorType = ModelErrorType.RateLimit;
            // 尝试通用解析
            result.RetryAfter = ExtractRetryAfterGeneric(headers, responseBody);
        }
        // 401/403 视为 AuthenticationError
        else if (statusCode == 401 || statusCode == 403)
        {
            result.ErrorType = ModelErrorType.AuthenticationError;
        }
        // 400 视为 BadRequest
        else if (statusCode == 400)
        {
            result.ErrorType = ModelErrorType.BadRequest;
        }

        return Task.FromResult(result);
    }

    // ── IResponseParser (abstract)

    public abstract ChatResponsePart? ParseChunk(string chunk);
    public abstract ChatResponsePart ParseCompleteResponse(string responseBody);

    // ── ExtractModelInfo + CreateDebugDownContext (abstract)

    public abstract void ExtractModelInfo(DownRequestContext down, Guid apiKeyId);
    public abstract DownRequestContext CreateDebugDownContext(string modelId, string message);

    // ── Account management (abstract)

    public abstract Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default);
    public abstract Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken ct = default);

    // ── Retry-After extraction

    protected TimeSpan? ExtractRetryAfterGeneric(
        Dictionary<string, IEnumerable<string>>? headers,
        string? body)
    {
        // 1. 检查 Standard Retry-After Header
        if (headers != null && headers.TryGetValue("Retry-After", out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(value))
            {
                // 尝试解析秒数
                if (double.TryParse(value, out var seconds))
                    return TimeSpan.FromSeconds(seconds);

                // 尝试解析 HTTP Date
                if (DateTime.TryParse(value, out var date))
                {
                    var delta = date - DateTime.UtcNow;
                    return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
                }
            }
        }

        if (string.IsNullOrEmpty(body)) return null;

        var patterns = new[]
        {
            @"retryDelay[""]?\s*:\s*[""]?(\d+(?:\.\d+)?)[""]?s",    // retryDelay: "8085.070001278s"
            @"quotaResetDelay[""]?\s*:\s*[""]?([\dhms.]+)[""]?",    // quotaResetDelay: "2h14m45.070001278s"
            @"retry[-_]?after[:\s=]+(\d+)",                          // retry-after: 60
            @"retry\s+after\s+(\d+)\s*seconds?",                     // retry after 60 seconds
            @"wait\s+(\d+)\s*seconds?",                              // wait 60 seconds
            @"(\d+)\s*seconds?\s+later",                             // 60 seconds later
            @"x-ratelimit-reset:\s*(\d+)"                            // x-ratelimit-reset: 1234567890
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(body, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = match.Groups[1].Value;

                // 尝试解析为秒数
                if (int.TryParse(value, out var intValue))
                {
                    // 如果值很大，可能是 Unix 时间戳
                    if (intValue > 1000000000)
                    {
                        var resetTime = DateTimeOffset.FromUnixTimeSeconds(intValue).UtcDateTime;
                        var seconds = (resetTime - DateTime.UtcNow).TotalSeconds;
                        return seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
                    }
                    return TimeSpan.FromSeconds(intValue);
                }

                // 尝试解析为时长格式 (如 "2h14m45s")
                var durationSeconds = ParseDurationToSeconds(value);
                if (durationSeconds.HasValue)
                    return TimeSpan.FromSeconds(durationSeconds.Value);
            }
        }

        return null;
    }

    protected int? ParseDurationToSeconds(string? duration)
    {
        if (string.IsNullOrEmpty(duration))
            return null;

        // 格式 1: "8085.070001278s" (仅含秒，无 h/m)
        if (duration.EndsWith("s") && !duration.Contains("h") && !duration.Contains("m"))
        {
            var secondsStr = duration.TrimEnd('s');
            if (double.TryParse(secondsStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                return (int)Math.Ceiling(seconds);
            return null; // 格式匹配但解析失败，不 fall-through 到格式2
        }

        // 格式 2: "2h14m45.070001278s" (复合格式)
        int totalSeconds = 0;

        var hourMatch = Regex.Match(duration, @"(\d+)h");
        if (hourMatch.Success && int.TryParse(hourMatch.Groups[1].Value, out var hours))
            totalSeconds += hours * 3600;

        var minuteMatch = Regex.Match(duration, @"(\d+)m");
        if (minuteMatch.Success && int.TryParse(minuteMatch.Groups[1].Value, out var minutes))
            totalSeconds += minutes * 60;

        var secondMatch = Regex.Match(duration, @"(\d+(?:\.\d+)?)s");
        if (secondMatch.Success && double.TryParse(secondMatch.Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var secs))
            totalSeconds += (int)Math.Ceiling(secs);

        return totalSeconds > 0 ? totalSeconds : null;
    }

    // ── Session Hash Generation

    protected string GenerateSessionHashWithContext(
        string messageContent,
        DownRequestContext downContext,
        Guid apiKeyId)
    {
        if (string.IsNullOrEmpty(messageContent))
            return GenerateFallbackSessionId();

        var combined = new StringBuilder();
        combined.Append(downContext.GetUserAgent() ?? string.Empty);
        combined.Append(':');
        combined.Append(apiKeyId.ToString());
        combined.Append('|');
        combined.Append(messageContent);

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined.ToString()));
        var hash = Convert.ToHexString(bytes).ToLowerInvariant();

        return $"sid-{hash[..SessionHashLength]}";
    }

    protected static string GenerateFallbackSessionId()
    {
        return $"sid-{Guid.NewGuid():N}"[..FallbackSessionIdLength];
    }

    /// <summary>
    /// 提取带 cache_control: ephemeral 的内容（Prompt Caching 支持）
    /// </summary>
    protected static string? ExtractCacheableContent(JsonNode? root)
    {
        if (root is not JsonObject rootObj) return null;

        if (rootObj.TryGetPropertyValue("system", out var systemNode) &&
            systemNode is JsonArray systemArray)
        {
            foreach (var part in systemArray)
            {
                if (part is not JsonObject partObj) continue;
                if (partObj.TryGetPropertyValue("cache_control", out var cacheControlNode) &&
                    cacheControlNode is JsonObject cacheControl &&
                    cacheControl.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue &&
                    typeValue.TryGetValue<string>(out var type) &&
                    type == "ephemeral")
                {
                    if (partObj.TryGetPropertyValue("text", out var textNode) &&
                        textNode is JsonValue textValue &&
                        textValue.TryGetValue<string>(out var text) &&
                        !string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        if (rootObj.TryGetPropertyValue("messages", out var messagesNode) &&
            messagesNode is JsonArray messagesArray)
        {
            foreach (var message in messagesArray)
            {
                if (message is not JsonObject messageObj) continue;
                if (messageObj.TryGetPropertyValue("content", out var contentNode) &&
                    contentNode is JsonArray contentArray)
                {
                    foreach (var part in contentArray)
                    {
                        if (part is not JsonObject partObj) continue;
                        if (partObj.TryGetPropertyValue("cache_control", out var cacheControlNode) &&
                            cacheControlNode is JsonObject cacheControl &&
                            cacheControl.TryGetPropertyValue("type", out var typeNode) &&
                            typeNode is JsonValue typeValue &&
                            typeValue.TryGetValue<string>(out var type) &&
                            type == "ephemeral")
                        {
                            return ExtractTextFromMessageContent(message);
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractTextFromMessageContent(JsonNode? message)
    {
        if (message is not JsonObject messageObj ||
            !messageObj.TryGetPropertyValue("content", out var contentNode))
            return null;

        if (contentNode is JsonValue contentValue &&
            contentValue.TryGetValue<string>(out var contentStr))
            return contentStr;

        if (contentNode is JsonArray contentArray)
        {
            var builder = new StringBuilder();
            foreach (var part in contentArray)
            {
                if (part is not JsonObject partObj) continue;
                if (partObj.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue &&
                    typeValue.TryGetValue<string>(out var type) &&
                    type == "text" &&
                    partObj.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text) &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    builder.Append(text);
                }
            }
            return builder.Length > 0 ? builder.ToString() : null;
        }

        return null;
    }
}

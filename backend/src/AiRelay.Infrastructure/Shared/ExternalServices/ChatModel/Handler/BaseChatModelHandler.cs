using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public abstract class BaseChatModelHandler : IChatModelHandler
{
    private const int SessionHashLength = 16;
    private const int FallbackSessionIdLength = 19;
    protected const int BackoffBaseSeconds = 5;
    protected const int BackoffMaxSeconds = 3600;

    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly SseResponseStreamProcessor StreamProcessor;
    protected readonly ISignatureCache SignatureCache;
    protected readonly UsageLoggingOptions LoggingOptions;
    protected readonly ILogger Logger;

    protected ChatModelConnectionOptions ConnectionOptions = null!;

    protected BaseChatModelHandler(
        IHttpClientFactory httpClientFactory,
        SseResponseStreamProcessor streamProcessor,
        ISignatureCache signatureCache,
        IOptions<UsageLoggingOptions> loggingOptions,
        ILogger logger)
    {
        HttpClientFactory = httpClientFactory;
        StreamProcessor = streamProcessor;
        SignatureCache = signatureCache;
        LoggingOptions = loggingOptions.Value;
        Logger = logger;
    }

    public abstract bool Supports(ProviderPlatform platform);

    public virtual void Configure(ChatModelConnectionOptions options)
    {
        ConnectionOptions = options;
    }

    public virtual string GetBaseUrl()
    {
        if (ConnectionOptions == null || string.IsNullOrEmpty(ConnectionOptions.BaseUrl))
        {
            return GetDefaultBaseUrl();
        }
        return ConnectionOptions.BaseUrl;
    }

    /// <summary>
    /// 获取平台默认 API 地址
    /// </summary>
    public abstract string GetDefaultBaseUrl();

    public virtual string? GetFallbackBaseUrl(int statusCode)
    {
        return null;
    }

    /// <summary>
    /// 分析错误响应（默认实现）
    /// </summary>
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

    /// <summary>
    /// 通用重试时间提取 (Headers + Body Regex)
    /// </summary>
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
                {
                    return TimeSpan.FromSeconds(seconds);
                }
                // 尝试解析 HTTP Date
                if (DateTime.TryParse(value, out var date))
                {
                    var delta = date - DateTime.UtcNow;
                    return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
                }
            }
        }

        if (string.IsNullOrEmpty(body)) return null;

        // 2. 正则匹配 Body (兜底)
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

    /// <summary>
    /// 解析时长字符串为秒数，支持：
    ///   格式1 - 纯秒数: "8085.070001278s"
    ///   格式2 - 复合格式: "2h14m45.070001278s"
    /// </summary>
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

    public abstract Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken cancellationToken = default);
    public abstract Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建调试用的标准请求上下文
    /// </summary>
    public abstract DownRequestContext CreateDebugDownContext(string modelId, string message);

    // ==================== HTTP Forwarding & Debug Methods ====================

    protected virtual async Task<HttpResponseMessage> ExecuteHttpAsync(
        UpRequestContext upContext,
        CancellationToken cancellationToken = default)
    {
        var httpClient = HttpClientFactory.CreateClient();

        var baseUrl = upContext.BaseUrl.EndsWith('/') ? upContext.BaseUrl : upContext.BaseUrl + "/";
        var relativeUrl = upContext.RelativePath.TrimStart('/') + (upContext.QueryString ?? "");

        // 直接在构造函数中传入完整 URI，避免 Uri 解析问题
        var request = new HttpRequestMessage(upContext.Method, baseUrl + relativeUrl);

        foreach (var header in upContext.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (upContext.HttpContent != null)
        {
            request.Content = upContext.HttpContent;
        }

        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    public virtual async Task<HttpResponseMessage> ExecuteHttpRequestAsync(
        UpRequestContext upContext,
        CancellationToken cancellationToken = default)
    {
        var currentDestination = GetBaseUrl();
        var hasTriedFallback = false;

        while (true)
        {
            try
            {
                var upContextWithDestination = upContext with { BaseUrl = currentDestination };
                var response = await ExecuteHttpAsync(upContextWithDestination, cancellationToken);

                if (response.IsSuccessStatusCode)
                    return response;

                var fallbackUrl = GetFallbackBaseUrl((int)response.StatusCode);
                if (fallbackUrl != null && !hasTriedFallback)
                {
                    Logger.LogWarning("端点异常 ({StatusCode})，切换备用端点", response.StatusCode);
                    response.Dispose();
                    hasTriedFallback = true;
                    currentDestination = fallbackUrl;
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



    // ==================== IRequestTransformer Abstract Implementation ====================

    public abstract void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId);

    public abstract Task<TransformedRequestContext> TransformProtocolAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken = default);

    public abstract Task<UpRequestContext> BuildHttpRequestAsync(
        DownRequestContext downContext,
        TransformedRequestContext transformedContext,
        CancellationToken cancellationToken = default);

    // ==================== IRequestEnricher Virtual Implementation ====================

    /// <summary>
    /// 代理专属增强（默认空实现）：各平台按需覆盖
    /// 仅在 SmartReverseProxy 场景由中间件显式调用，DebugModel 场景不调用
    /// </summary>
    public virtual void ApplyProxyEnhancements(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        return;
    }

    // ==================== IResponseParser Abstract Implementation ====================

    public abstract ChatResponsePart? ParseChunk(string chunk);

    public abstract ChatResponsePart ParseCompleteResponse(string responseBody);

    // ==================== Signature Extraction (Optional Override) ====================

    protected virtual Action<string>? GetSignatureExtractor(string? sessionId)
    {
        return null;
    }

    // ==================== Session Hash Generation (Migrated from SessionIdHelper) ====================

    /// <summary>
    /// 生成会话哈希（混入上下文，防止碰撞）
    /// </summary>
    protected string GenerateSessionHashWithContext(
        string messageContent,
        DownRequestContext downContext,
        Guid apiKeyId)
    {
        if (string.IsNullOrEmpty(messageContent))
        {
            return GenerateFallbackSessionId();
        }

        var combined = new StringBuilder();
        // 移除 ClientIp 以避免 IP 变化导致会话粘性丢失（移动网络、VPN切换等场景）
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
                        textValue.TryGetValue<string>(out var text))
                    {
                        if (!string.IsNullOrWhiteSpace(text)) return text;
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

        var builder = new StringBuilder();
        if (contentNode is JsonValue contentValue &&
            contentValue.TryGetValue<string>(out var contentStr))
            return contentStr;

        if (contentNode is JsonArray contentArray)
        {
            foreach (var part in contentArray)
            {
                if (part is not JsonObject partObj) continue;
                if (partObj.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue &&
                    typeValue.TryGetValue<string>(out var type) &&
                    type == "text")
                {
                    if (partObj.TryGetPropertyValue("text", out var textNode) &&
                        textNode is JsonValue textValue &&
                        textValue.TryGetValue<string>(out var text))
                    {
                        if (!string.IsNullOrWhiteSpace(text)) builder.Append(text);
                    }
                }
            }
        }
        return builder.Length > 0 ? builder.ToString() : null;
    }
}

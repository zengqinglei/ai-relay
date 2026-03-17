using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// Google 内部聊天模型客户端基类
/// </summary>
public abstract class GoogleInternalChatModelHandlerBase(
    IHttpClientFactory httpClientFactory,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    IOptions<UsageLoggingOptions> loggingOptions,
    ILogger logger)
    : BaseChatModelHandler(httpClientFactory, streamProcessor, signatureCache, loggingOptions, logger)
{

    protected override Action<string>? GetSignatureExtractor(string? sessionId)
    {
        return string.IsNullOrEmpty(sessionId) ? null : line => TryExtractAndCacheSignature(line, sessionId);
    }

    public override string GetDefaultBaseUrl()
    {
        return "https://cloudcode-pa.googleapis.com";
    }

    public override async Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody)
    {
        // 429/503 限流 / 容量不足
        if (statusCode == 429 || statusCode == 503)
        {
            var result = new ModelErrorAnalysisResult
            {
                ErrorType = ModelErrorType.RateLimit,
                IsRetryableOnSameAccount = IsModelCapacityExhausted(responseBody), // 容量耗尽时同账号重试
                RequiresDowngrade = false,
                RetryAfter = null
            };

            // Google JSON 错误格式解析 (优先级高于通用解析)
            result.RetryAfter = ExtractRetryDelayFromGoogleError(responseBody) ??
                                ExtractRetryAfterGeneric(headers, responseBody);

            return result;
        }

        return await base.AnalyzeErrorAsync(statusCode, headers, responseBody);
    }

    /// <summary>
    /// 检查是否为模型容量耗尽错误（适合同账号重试）
    /// </summary>
    private static bool IsModelCapacityExhausted(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return false;

        try
        {
            var json = JsonNode.Parse(responseBody);
            if (json is not JsonObject jsonObj) return false;

            // 检查 error.details[].reason == "MODEL_CAPACITY_EXHAUSTED"
            var details = jsonObj["error"]?["details"] as JsonArray;
            if (details != null)
            {
                foreach (var detail in details)
                {
                    if (detail is JsonObject detailObj)
                    {
                        var reason = detailObj["reason"]?.GetValue<string>();
                        if (reason == "MODEL_CAPACITY_EXHAUSTED")
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // 解析失败，默认不是容量耗尽
        }

        return false;
    }

    protected TimeSpan? ExtractRetryDelayFromGoogleError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            var details = JsonNode.Parse(body)?["error"]?["details"] as JsonArray;
            if (details == null) return null;

            foreach (var detail in details.OfType<JsonObject>())
            {
                // 1. 检查 RetryInfo.retryDelay
                if (detail["@type"]?.GetValue<string>()?.Contains("RetryInfo") == true)
                {
                    var delayStr = detail["retryDelay"]?.GetValue<string>();
                    if (TryParseGoogleDuration(delayStr, out var ts)) return ts;
                }

                // 2. 检查 metadata 中的延迟信息
                if (detail["metadata"] is JsonObject metadata)
                {
                    // quotaResetDelay
                    var delayStr = metadata["quotaResetDelay"]?.GetValue<string>();
                    if (TryParseGoogleDuration(delayStr, out var ts)) return ts;

                    // quotaResetTimeStamp
                    var timestampStr = metadata["quotaResetTimeStamp"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(timestampStr) &&
                        DateTime.TryParse(timestampStr, null, DateTimeStyles.RoundtripKind, out var resetTime))
                    {
                        var remaining = resetTime.ToUniversalTime() - DateTime.UtcNow;
                        if (remaining > TimeSpan.Zero) return remaining;
                    }
                }
            }
        }
        catch
        {
            // 解析失败忽略
        }
        return null;
    }

    protected static bool TryParseGoogleDuration(string? duration, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrEmpty(duration)) return false;

        // 简单纯秒格式: "3.5s"
        if (!duration.Contains('h') && !duration.Contains('m'))
        {
            var raw = duration.TrimEnd('s');
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var simpleSeconds))
            {
                result = TimeSpan.FromSeconds(simpleSeconds);
                return true;
            }
            return false;
        }

        // 复合时长格式: "2h14m45.070001278s"
        double total = 0;
        var hourMatch = Regex.Match(duration, @"(\d+)h");
        if (hourMatch.Success) total += int.Parse(hourMatch.Groups[1].Value) * 3600;

        var minuteMatch = Regex.Match(duration, @"(\d+)m");
        if (minuteMatch.Success) total += int.Parse(minuteMatch.Groups[1].Value) * 60;

        var secondMatch = Regex.Match(duration, @"(\d+(?:\.\d+)?)s");
        if (secondMatch.Success &&
            double.TryParse(secondMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var compoundSeconds))
        {
            total += compoundSeconds;
        }

        if (total > 0)
        {
            result = TimeSpan.FromSeconds(total);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 通过 LoadCodeAssist 接口获取 Project ID
    /// </summary>
    protected async Task<LoadCodeAssistResponse?> LoadCodeAssistAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var httpClient = HttpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/v1internal:loadCodeAssist");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            metadata = new
            {
                ideType = "ANTIGRAVITY"
            }
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoadCodeAssistResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: cancellationToken);

        return result;
    }

    /// <summary>
    /// 获取账户配额信息（Google Code Assist API）
    /// </summary>
    protected async Task<AccountQuotaInfo?> FetchQuotaInternalAsync(string accessToken, string? projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = HttpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/v1internal:fetchAvailableModels");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var requestBody = new { project = projectId ?? "" };
            request.Content = JsonContent.Create(requestBody);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // 从 models 中提取配额信息
            if (!doc.RootElement.TryGetProperty("models", out var models)) return null;

            double? remainingFraction = null;
            string? resetTime = null;

            foreach (var model in models.EnumerateObject())
            {
                if (model.Value.TryGetProperty("quotaInfo", out var quotaInfo))
                {
                    if (quotaInfo.TryGetProperty("remainingFraction", out var fraction))
                        remainingFraction = fraction.GetDouble();

                    if (quotaInfo.TryGetProperty("resetTime", out var reset))
                        resetTime = reset.GetString();

                    break; // 取第一个模型的配额信息
                }
            }

            return new AccountQuotaInfo
            {
                RemainingQuota = remainingFraction.HasValue ? (int)(remainingFraction.Value * 100) : null,
                QuotaResetTime = resetTime,
                SubscriptionTier = null,
                LastRefreshed = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    protected class LoadCodeAssistResponse
    {
        public string? CloudaicompanionProject { get; set; }
        public List<TierInfo>? AllowedTiers { get; set; }
        public List<IneligibleTierInfo>? IneligibleTiers { get; set; }
    }

    protected class TierInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    protected class IneligibleTierInfo
    {
        public string? ReasonCode { get; set; }
        public string? ReasonMessage { get; set; }
        public string? TierId { get; set; }
        public string? TierName { get; set; }
    }

    protected void TryExtractAndCacheSignature(string line, string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !line.StartsWith("data:")) return;

        var json = line.Substring(5).TrimStart();
        if (json == "[DONE]") return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("response", out var responseObj))
                root = responseObj;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts))
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("thoughtSignature", out var sig))
                        {
                            var signature = sig.GetString();
                            if (!string.IsNullOrEmpty(signature))
                            {
                                SignatureCache.CacheSignature(sessionId, signature);
                                Logger.LogDebug("提取并缓存签名 Session: {Session}", sessionId);
                                return;
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }
}

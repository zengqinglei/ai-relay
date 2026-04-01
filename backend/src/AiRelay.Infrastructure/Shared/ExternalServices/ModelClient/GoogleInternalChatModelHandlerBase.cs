using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.SignatureCache;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Common;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

/// <summary>
/// Google 内部聊天模型客户端基类
/// </summary>
public abstract class GoogleInternalChatModelHandlerBase(
    ChatModelConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    ISignatureCache signatureCache,
    ILogger logger)
    : BaseChatModelHandler(options, httpClientFactory, logger)
{
    protected override string? GetFallbackBaseUrl(int statusCode)
    {
        if (statusCode == 429 || statusCode == 408 || statusCode == 404 ||
            (statusCode >= 500 && statusCode < 600))
            return "https://daily-cloudcode-pa.sandbox.googleapis.com";
        return null;
    }

    protected override IReadOnlyList<IResponseProcessor> GetResponseProcessors(
        UpRequestContext up, DownRequestContext down)
    {
        var processors = new List<IResponseProcessor>
        {

            new GoogleParseSseResponseProcessor(),
            new UsageAccumulatorResponseProcessor(),
            new GoogleCacheSignatureResponseProcessor(signatureCache, up.SessionId, Logger)
        };
        return processors;
    }

    public override async Task<ModelErrorAnalysisResult> CheckRetryPolicyAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string? responseBody)
    {
        // 签名错误 → 降级重试
        if (statusCode == 400 && GoogleSignatureCleaner.IsSignatureError(responseBody))
        {
            return new ModelErrorAnalysisResult { IsCanRetry = true, RequiresDowngrade = true };
        }

        // 429/503 限流 / 容量不足
        if (statusCode == 429 || statusCode == 503)
        {
            var retryAfter = ExtractRetryAfter(headers, responseBody);

            return new ModelErrorAnalysisResult { IsCanRetry = true, RetryAfter = retryAfter };
        }

        return await base.CheckRetryPolicyAsync(statusCode, headers, responseBody);
    }

    protected override TimeSpan? ExtractRetryAfter(Dictionary<string, IEnumerable<string>>? headers, string? body)
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
        return base.ExtractRetryAfter(headers, body);
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

    public override async Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { metadata = new { ideType = "ANTIGRAVITY" } });
            var down = new DownRequestContext
            {
                Method = HttpMethod.Post,
                RelativePath = "/v1internal:loadCodeAssist",
                BodyBytes = Encoding.UTF8.GetBytes(body).AsMemory()
            };
            var up = await ProcessRequestContextAsync(down, 0, ct);
            using var response = await SendRequestAsync(up, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LoadCodeAssistResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            if (result == null)
                return new ConnectionValidationResult(false, "LoadCodeAssist 返回空响应");
            if (!string.IsNullOrEmpty(result.CloudaicompanionProject))
                return new ConnectionValidationResult(true, ProjectId: result.CloudaicompanionProject);
            if (result.IneligibleTiers?.Count > 0)
            {
                var ineligible = result.IneligibleTiers[0];
                return new ConnectionValidationResult(false,
                    !string.IsNullOrEmpty(ineligible.ReasonMessage)
                        ? ineligible.ReasonMessage
                        : $"Account is not eligible for {ineligible.TierName ?? "Gemini Code Assist"}");
            }
            if (result.AllowedTiers?.Count > 0)
                return new ConnectionValidationResult(false,
                    $"Your account is registered for {result.AllowedTiers[0].Name ?? "Gemini Code Assist"}, but no project_id is configured.");

            return new ConnectionValidationResult(false, "获取 Code Assist 项目失败：未返回 project_id");
        }
        catch (Exception ex)
        {
            return new ConnectionValidationResult(false, $"认证失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 从 Gemini 消息的 parts 数组中提取文本
    /// </summary>
    protected static string ExtractTextFromParts(JsonNode? content)
    {
        if (content is not JsonObject contentObj ||
            !contentObj.TryGetPropertyValue("parts", out var partsNode) ||
            partsNode is not JsonArray parts)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part is JsonObject partObj &&
                partObj.TryGetPropertyValue("text", out var textNode) &&
                textNode is JsonValue textValue &&
                textValue.TryGetValue<string>(out var text))
            {
                sb.Append(text);
            }
        }
        return sb.ToString();
    }

    private sealed class LoadCodeAssistResponse
    {
        public string? CloudaicompanionProject { get; set; }
        public List<TierInfo>? AllowedTiers { get; set; }
        public List<IneligibleTierInfo>? IneligibleTiers { get; set; }
    }
    private sealed class TierInfo { public string? Name { get; set; } }
    private sealed class IneligibleTierInfo
    {
        public string? ReasonMessage { get; set; }
        public string? TierName { get; set; }
    }
}

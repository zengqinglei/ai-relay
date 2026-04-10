using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Buffer;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

public abstract class BaseChatModelHandler : IChatModelHandler
{
    private const int SessionHashLength = 16;
    private const int FallbackSessionIdLength = 19;
    protected const int BackoffBaseSeconds = 5;
    protected const int BackoffMaxSeconds = 3600;

    protected readonly ChatModelConnectionOptions Options;
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly ILogger Logger;

    protected BaseChatModelHandler(
        ChatModelConnectionOptions options,
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        Options = options;
        HttpClientFactory = httpClientFactory;
        Logger = logger;
    }

    public abstract bool Supports(Provider provider, AuthMethod authMethod);

    protected virtual string? GetFallbackBaseUrl(int statusCode) => null;

    /// <summary>
    /// 子类根据请求路由和降级级别返回 Request Processor 列表
    /// </summary>
    protected abstract IReadOnlyList<IRequestProcessor> GetRequestProcessors(
        DownRequestContext down,
        int degradationLevel);

    /// <summary>
    /// 子类返回 Response Processor 列表（按平台 + 请求上下文定制）
    /// </summary>
    protected abstract IReadOnlyList<IResponseProcessor> GetResponseProcessors(
        UpRequestContext up,
        DownRequestContext down);

    // ── ProcessRequestContextAsync

    public async Task<UpRequestContext> ProcessRequestContextAsync(
        DownRequestContext down,
        int degradationLevel = 0,
        CancellationToken ct = default)
    {
        var up = new UpRequestContext { Method = down.Method, MappedModelId = down.ModelId };

        foreach (var processor in GetRequestProcessors(down, degradationLevel))
        {
            await processor.ProcessAsync(down, up, ct);
        }

        return up;
    }

    // ── SendCoreRequestAsync（含 Fallback 重试，仅供内部及子类调用）

    protected async Task<HttpResponseMessage> SendCoreRequestAsync(UpRequestContext up, DownRequestContext down, CancellationToken ct = default)
    {
        var hasTriedFallback = false;

        while (true)
        {
            using var httpClient = HttpClientFactory.CreateClient("ModelProxyClient");

            // 统一交由 UpRequestContext 中心化构建请求报文
            using var request = up.BuildHttpRequestMessage(down);

            // 发送请求
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
                return response;

            // 检查是否有备用 URL 可切换
            var fallbackUrl = GetFallbackBaseUrl((int)response.StatusCode);
            if (fallbackUrl != null && !hasTriedFallback)
            {
                Logger.LogWarning("端点异常 ({StatusCode})，切换备用端点", response.StatusCode);
                response.Dispose();
                hasTriedFallback = true;
                // 同步更新上下文的 BaseUrl 以供下一轮 BuildHttpRequestMessage 使用
                up.BaseUrl = fallbackUrl;
                continue;
            }

            return response;
        }
    }

    // ── SendChatRequestAsync（两阶段：Phase 1 发送 + Phase 2 流式消费）

    /// <summary>
    /// Phase 1：发送请求，等待响应头返回。
    /// 成功时 ProxyResponse.Events 携带懒加载事件流；
    /// 失败时 ProxyResponse.ErrorBody 携带错误体，HttpResponseMessage 已 Dispose。
    /// </summary>
    public async Task<ProxyResponse> SendChatRequestAsync(
        UpRequestContext up,
        DownRequestContext down,
        bool isStreaming,
        CancellationToken ct = default)
    {
        var response = await SendCoreRequestAsync(up, down, ct);
        var statusCode = (int)response.StatusCode;
        var headers = ExtractResponseHeaders(response);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();
            return new ProxyResponse(false, statusCode, headers, errorBody, null);
        }

        // Phase 2：状态机闭包持有 response，finally 块负责 Dispose
        // 统一走流式路径：非流式 JSON 响应体通过 Fast-Pass 的 OriginalBytes 整块透传，与流式路径等价
        var processors = GetResponseProcessors(up, down);

        async IAsyncEnumerable<StreamEvent> StreamEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                var sseBuffer = new SseStreamBuffer();
                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = ArrayPool<byte>.Shared.Rent(8192);

                bool requiresMutation = processors.Any(p => p.RequiresMutation);

                try
                {
                    int bytesRead;
                    while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        if (!requiresMutation)
                            yield return new StreamEvent { OriginalBytes = buffer.AsSpan(0, bytesRead).ToArray() };

                        var lines = sseBuffer.ProcessChunk(buffer.AsSpan(0, bytesRead));
                        await foreach (var evt in ProcessLinesAsync(lines))
                        {
                            yield return evt;
                            if (evt.IsComplete) yield break;
                        }
                    }

                    await foreach (var evt in ProcessLinesAsync(sseBuffer.Flush()))
                    {
                        yield return evt;
                        if (evt.IsComplete) yield break;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                async IAsyncEnumerable<StreamEvent> ProcessLinesAsync(IEnumerable<string> lines)
                {
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            if (requiresMutation) yield return new StreamEvent { OriginalBytes = "\n"u8.ToArray() };
                            continue;
                        }

                        var evt = new StreamEvent { SseLine = line };
                        if (requiresMutation)
                        {
                            evt.OriginalBytes = System.Text.Encoding.UTF8.GetBytes(line + "\n\n");
                        }
                        foreach (var proc in processors) await proc.ProcessAsync(evt, cancellationToken);

                        if (evt.Content != null || evt.InlineData != null || evt.IsComplete
                            || evt.Type == StreamEventType.Error || evt.Usage != null
                            || evt.HasOutput
                            || evt.OriginalBytes != null || evt.ConvertedBytes != null)
                        {
                            yield return evt;
                        }
                    }
                }

                // 补发完成事件（ConvertedBytes/OriginalBytes=null，让中间件补发 [DONE]）
                var completeEvt = new StreamEvent { IsComplete = true };
                foreach (var proc in processors) await proc.ProcessAsync(completeEvt, cancellationToken);
                yield return completeEvt;
            }
            finally
            {
                // 确保 HttpResponseMessage 在枚举完成（含取消）后正确释放
                response.Dispose();
            }
        }

        return new ProxyResponse(true, statusCode, headers, null, StreamEventsAsync(ct));
    }

    // ── Retry Policy

    public virtual Task<ModelErrorAnalysisResult> CheckRetryPolicyAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string? responseBody)
    {
        var isOfficialAccount = string.IsNullOrEmpty(Options.BaseUrl);
        var result = new ModelErrorAnalysisResult();

        if (statusCode == 429 || statusCode == 503) // 429/503 限流 / 容量不足
        {
            var retryAfter = ExtractRetryAfter(headers, responseBody);
            result.RetryAfter = retryAfter;
            result.IsCanRetry = true;
        }
        else
        {
            // 官方账号 5xx：不允许同账号重试，由外层换号
            // 非官方账号 5xx：允许重试（临时故障）
            result.IsCanRetry = !isOfficialAccount;
            result.RetryAfter = result.IsCanRetry ? ExtractRetryAfter(headers, responseBody) : null;
        }

        return Task.FromResult(result);
    }

    public abstract void ExtractModelInfo(DownRequestContext down, Guid apiKeyId);
    public abstract DownRequestContext CreateDebugDownContext(string modelId, string message);
    public virtual Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default) =>
        Task.FromResult(new ConnectionValidationResult(true));
    public virtual Task<IReadOnlyList<AccountQuotaInfo>?> FetchQuotaAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AccountQuotaInfo>?>(null);

    /// <summary>
    /// 从上游 API 拉取可用模型列表（默认不支持，返回 null）
    /// </summary>
    public virtual Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ModelOption>?>(null);
    }

    protected virtual TimeSpan? ExtractRetryAfter(Dictionary<string, IEnumerable<string>>? headers, string? body)
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

    // ── Helper: Extract response headers

    private static Dictionary<string, IEnumerable<string>> ExtractResponseHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
            headers[header.Key] = header.Value;
        if (response.Content?.Headers != null)
            foreach (var header in response.Content.Headers)
                headers[header.Key] = header.Value;
        return headers;
    }
}

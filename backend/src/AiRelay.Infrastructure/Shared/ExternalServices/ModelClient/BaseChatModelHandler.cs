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

public abstract partial class BaseChatModelHandler : IChatModelHandler
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

    // ═══════════════════════════════════════════════════════════════════
    // ProcessRequestContextAsync
    // ═══════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════
    // SendCoreRequestAsync（含 Fallback 重试，仅供内部及子类调用）
    // ═══════════════════════════════════════════════════════════════════

    protected async Task<HttpResponseMessage> SendCoreRequestAsync(
        UpRequestContext up, DownRequestContext down, CancellationToken ct = default)
    {
        var hasTriedFallback = false;

        while (true)
        {
            using var httpClient = HttpClientFactory.CreateClient("ModelProxyClient");

            // 统一交由 UpRequestContext 中心化构建请求报文
            using var request = up.BuildHttpRequestMessage(down);

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
                up.BaseUrl = fallbackUrl;
                continue;
            }

            return response;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // SendChatRequestAsync（Phase 1：发送请求，等待响应头）
    // ═══════════════════════════════════════════════════════════════════

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
        HttpResponseMessage response;
        try
        {
            response = await SendCoreRequestAsync(up, down, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 非客户端主动取消 → 视为上游响应超时
            return new ProxyResponse(false, 504, new(), "上游请求超时 (Gateway Timeout)", null);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is System.Net.Sockets.SocketException)
        {
            // 网络传输层异常（SSL 握手失败、DNS 失败、连接重置等）→ 转译为 502，触发中间件重试/切号
            var errorMsg = $"网络传输层异常 ({ex.GetType().Name}): {ex.Message}";
            Logger.LogWarning(ex, errorMsg);
            return new ProxyResponse(false, 502, [], errorMsg, null);
        }

        var statusCode = (int)response.StatusCode;
        var headers = ExtractResponseHeaders(response);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();
            return new ProxyResponse(false, statusCode, headers, errorBody, null);
        }

        // Phase 2：懒加载事件流；非流式响应通过 Fast-Pass 整块透传，与流式路径等价
        var processors = GetResponseProcessors(up, down);
        return new ProxyResponse(true, statusCode, headers, null, StreamEventsAsync(response, processors, ct));
    }

    // ═══════════════════════════════════════════════════════════════════
    // StreamEventsAsync（Phase 2：读取上游字节流，解析事件）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 持有 <paramref name="response"/> 所有权，在 finally 中 Dispose。
    /// <para>
    /// <b>双路径设计</b>：当 <c>requiresMutation = false</c> 时，原始字节以 Fast-Pass 方式整块透传，
    /// 同时仍执行 SSE 解析以采集 Usage / IsComplete 元数据（透传事件的 OriginalBytes ≠ null，
    /// 元数据事件的 OriginalBytes/ConvertedBytes = null，中间件侧通过 null 判断跳过转发）。
    /// </para>
    /// </summary>
    private async IAsyncEnumerable<StreamEvent> StreamEventsAsync(
        HttpResponseMessage response,
        IReadOnlyList<IResponseProcessor> processors,
        [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            var sseBuffer = new SseStreamBuffer();
            await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = ArrayPool<byte>.Shared.Rent(8192);
            bool requiresMutation = processors.Any(p => p.RequiresMutation);

            try
            {
                int bytesRead;
                while ((bytesRead = await responseStream.ReadAsync(buffer, ct)) > 0)
                {
                    // Fast-pass：非变换路径整块透传，SSE 解析仅用于 Usage/IsComplete 元数据采集
                    if (!requiresMutation)
                        yield return new StreamEvent { OriginalBytes = buffer.AsSpan(0, bytesRead).ToArray() };

                    var lines = sseBuffer.ProcessChunk(buffer.AsSpan(0, bytesRead));
                    await foreach (var evt in ProcessSseLinesAsync(lines, requiresMutation, processors, ct))
                    {
                        yield return evt;
                        if (evt.IsComplete) yield break;
                    }
                }

                await foreach (var evt in ProcessSseLinesAsync(sseBuffer.Flush(), requiresMutation, processors, ct))
                {
                    yield return evt;
                    if (evt.IsComplete) yield break;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            // 补发完成事件（ConvertedBytes/OriginalBytes=null，让中间件补发 [DONE]）
            var completeEvt = new StreamEvent { IsComplete = true };
            foreach (var proc in processors) await proc.ProcessAsync(completeEvt, ct);
            yield return completeEvt;
        }
        finally
        {
            // 确保 HttpResponseMessage 在枚举完成（含取消）后正确释放
            response.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ProcessSseLinesAsync（SSE 文本行 → StreamEvent 转换与过滤）
    // ═══════════════════════════════════════════════════════════════════

    private static async IAsyncEnumerable<StreamEvent> ProcessSseLinesAsync(
        IEnumerable<string> lines,
        bool requiresMutation,
        IReadOnlyList<IResponseProcessor> processors,
        [EnumeratorCancellation] CancellationToken ct)
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
                evt.OriginalBytes = Encoding.UTF8.GetBytes(line + "\n\n");
            }
            foreach (var proc in processors) await proc.ProcessAsync(evt, ct);

            if (evt.Content != null || evt.InlineData != null || evt.IsComplete
                || evt.Type == StreamEventType.Error || evt.Usage != null
                || evt.HasOutput
                || evt.OriginalBytes != null || evt.ConvertedBytes != null)
            {
                yield return evt;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Retry Policy
    // ═══════════════════════════════════════════════════════════════════

    public virtual Task<ModelErrorAnalysisResult> CheckRetryPolicyAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string? responseBody)
    {
        var isOfficialAccount = string.IsNullOrEmpty(Options.BaseUrl);
        var result = new ModelErrorAnalysisResult();

        if (statusCode == 429 || statusCode == 503) // 限流 / 容量不足
        {
            result.RetryAfter = ExtractRetryAfter(headers, responseBody);
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
    public virtual Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ModelOption>?>(null);

    // ═══════════════════════════════════════════════════════════════════
    // Retry-After 解析
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 优先从响应头解析，其次从响应体文本中匹配。
    /// </summary>
    protected virtual TimeSpan? ExtractRetryAfter(
        Dictionary<string, IEnumerable<string>>? headers, string? body)
        => ExtractRetryAfterFromHeader(headers) ?? ExtractRetryAfterFromBody(body);

    private static TimeSpan? ExtractRetryAfterFromHeader(Dictionary<string, IEnumerable<string>>? headers)
    {
        if (headers == null || !headers.TryGetValue("Retry-After", out var values)) return null;

        var value = values.FirstOrDefault();
        if (string.IsNullOrEmpty(value)) return null;

        // 尝试解析为秒数
        if (double.TryParse(value, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        // 尝试解析为 HTTP Date
        if (DateTime.TryParse(value, out var date))
        {
            var delta = date - DateTime.UtcNow;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        return null;
    }

    private static TimeSpan? ExtractRetryAfterFromBody(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;

        return TryMatchDuration(RetryDelayRegex(), body)         // retryDelay: "8085.07s"
            ?? TryMatchDuration(QuotaResetDelayRegex(), body)   // quotaResetDelay: "2h14m45s"
            ?? TryMatchDuration(RetryAfterRegex(), body)        // retry-after: 60
            ?? TryMatchDuration(RetryAfterSecondsRegex(), body) // retry after 60 seconds
            ?? TryMatchDuration(WaitSecondsRegex(), body)       // wait 60 seconds
            ?? TryMatchDuration(SecondsLaterRegex(), body)      // 60 seconds later
            ?? TryMatchDuration(RateLimitResetRegex(), body);   // x-ratelimit-reset: 1234567890
    }

    private static TimeSpan? TryMatchDuration(Regex regex, string body)
    {
        var match = regex.Match(body);
        if (!match.Success) return null;

        var value = match.Groups[1].Value;

        if (int.TryParse(value, out var intValue))
        {
            // 大数值视为 Unix 时间戳
            if (intValue > 1_000_000_000)
            {
                var wait = (DateTimeOffset.FromUnixTimeSeconds(intValue).UtcDateTime - DateTime.UtcNow).TotalSeconds;
                return wait > 0 ? TimeSpan.FromSeconds(wait) : TimeSpan.Zero;
            }
            return TimeSpan.FromSeconds(intValue);
        }

        var durationSeconds = ParseDurationToSeconds(value);
        return durationSeconds.HasValue ? TimeSpan.FromSeconds(durationSeconds.Value) : null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 时长字符串解析（"591ms" / "8085.07s" / "2h14m45s"）
    // ═══════════════════════════════════════════════════════════════════

    protected static int? ParseDurationToSeconds(string? duration)
    {
        if (string.IsNullOrEmpty(duration)) return null;

        // 1. 毫秒格式: "591.851946ms"
        if (duration.EndsWith("ms", StringComparison.Ordinal))
        {
            var msValue = duration[..^2]; // 去除 "ms" 后缀
            return double.TryParse(msValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)
                ? (int)Math.Ceiling(ms / 1000.0)
                : null;
        }

        // 2. 纯秒格式: "8085.070001278s"（不含 h/m 组件）
        if (duration.EndsWith('s') && !duration.Contains('h') && !duration.Contains('m'))
        {
            var secValue = duration[..^1]; // 去除 "s" 后缀
            return double.TryParse(secValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var secs)
                ? (int)Math.Ceiling(secs)
                : null;
        }

        // 3. 复合格式: "2h14m45.070001278s"
        // 负向断言确保匹配的单位前无小数点，且 m 不是 ms 的一部分
        int total = 0;

        var hourMatch = DurationHourRegex().Match(duration);
        if (hourMatch.Success && int.TryParse(hourMatch.Groups[1].Value, out var hours))
            total += hours * 3600;

        var minuteMatch = DurationMinuteRegex().Match(duration);
        if (minuteMatch.Success && int.TryParse(minuteMatch.Groups[1].Value, out var minutes))
            total += minutes * 60;

        var secondMatch = DurationSecondRegex().Match(duration);
        if (secondMatch.Success && double.TryParse(secondMatch.Groups[1].Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            total += (int)Math.Ceiling(seconds);

        return total > 0 ? total : null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Session Hash 生成
    // ═══════════════════════════════════════════════════════════════════

    protected string GenerateSessionHashWithContext(
        string messageContent,
        DownRequestContext downContext,
        Guid apiKeyId)
    {
        if (string.IsNullOrEmpty(messageContent))
            return GenerateFallbackSessionId();

        var combined = $"{downContext.GetUserAgent() ?? string.Empty}:{apiKeyId}|{messageContent}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combined))).ToLowerInvariant();
        return $"sid-{hash[..SessionHashLength]}";
    }

    protected static string GenerateFallbackSessionId() =>
        $"sid-{Guid.NewGuid():N}"[..FallbackSessionIdLength];

    // ═══════════════════════════════════════════════════════════════════
    // 通用辅助
    // ═══════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════
    // Source-Generated Regex（编译时生成，零运行时启动开销）
    // ═══════════════════════════════════════════════════════════════════

    // ── Retry-After 响应体模式 ──

    [GeneratedRegex(@"retryDelay[""']?\s*:\s*[""']?(\d+(?:\.\d+)?)[""']?s", RegexOptions.IgnoreCase)]
    private static partial Regex RetryDelayRegex();

    [GeneratedRegex(@"quotaResetDelay[""']?\s*:\s*[""']?([\dhms.]+)[""']?", RegexOptions.IgnoreCase)]
    private static partial Regex QuotaResetDelayRegex();

    [GeneratedRegex(@"retry[-_]?after[:\s=]+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RetryAfterRegex();

    [GeneratedRegex(@"retry\s+after\s+(\d+)\s*seconds?", RegexOptions.IgnoreCase)]
    private static partial Regex RetryAfterSecondsRegex();

    [GeneratedRegex(@"wait\s+(\d+)\s*seconds?", RegexOptions.IgnoreCase)]
    private static partial Regex WaitSecondsRegex();

    [GeneratedRegex(@"(\d+)\s*seconds?\s+later", RegexOptions.IgnoreCase)]
    private static partial Regex SecondsLaterRegex();

    [GeneratedRegex(@"x-ratelimit-reset:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RateLimitResetRegex();

    // ── 复合时长格式解析 ──

    [GeneratedRegex(@"(?<![\d.])(\d+)h")]
    private static partial Regex DurationHourRegex();

    [GeneratedRegex(@"(?<![\d.])(\d+)m(?!s)")]
    private static partial Regex DurationMinuteRegex();

    [GeneratedRegex(@"(?<![\d.])(\d+(?:\.\d+)?)s")]
    private static partial Regex DurationSecondRegex();
}

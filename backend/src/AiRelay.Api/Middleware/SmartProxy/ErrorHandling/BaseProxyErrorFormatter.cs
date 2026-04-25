using System.Net.Mime;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Exception.Core;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

public abstract class BaseProxyErrorFormatter : IProxyErrorFormatter
{
    public abstract bool Supports(RouteProfile profile);

    public ProxyErrorResponse Format(Exception exception)
    {
        var statusCode = ResolveStatusCode(exception);
        var message = exception.Message;

        // 如果是 503/429，注入诱导词
        if (statusCode == 503 || statusCode == 429)
        {
            message = EnrichMessage(message);
        }

        return BuildResponse(statusCode, message);
    }

    public virtual ProxyErrorResponse Normalize(int statusCode, string? upstreamBody)
    {
        // 1. 提取原始错误消息，确保诊断信息不丢失
        var originalMessage = TryExtractMessage(upstreamBody) ?? "Service Temporarily Unavailable";

        // 2. 注入针对 OpenClaw 的诱导词
        if (statusCode == 503 || statusCode == 429)
        {
            originalMessage = EnrichMessage(originalMessage);
        }

        return BuildResponse(statusCode, originalMessage);
    }

    private static string EnrichMessage(string message)
    {
        // 必须包含 "Service Unavailable" 和 "(Overloaded)" 才能通过正则表达式识别
        return message.Contains("(Overloaded)")
            ? message
            : $"Service Unavailable (Overloaded): {message}";
    }

    protected abstract ProxyErrorResponse BuildResponse(int statusCode, string message);

    protected virtual string? TryExtractMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            // 简单启发式解析 JSON 中的 message 字段 (兼容 OpenAI/Gemini/Claude 结构)
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var msg)) return msg.GetString();
            }

            return doc.RootElement.TryGetProperty("message", out var topMsg) ? topMsg.GetString() : body;
        }
        catch { /* 忽略解析错误，回退到原始文本 */ }

        return body;
    }

    protected static int ResolveStatusCode(Exception exception) => exception switch
    {
        BusinessException biz => int.Parse(biz.Code.ToString()[..3]),
        OperationCanceledException { InnerException: TimeoutException } => 503,
        TimeoutException => 503,
        HttpRequestException => 503,
        _ => 500
    };
}

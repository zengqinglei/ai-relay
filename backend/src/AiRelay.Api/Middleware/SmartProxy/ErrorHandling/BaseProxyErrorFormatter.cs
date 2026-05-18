using AiRelay.Domain.Shared.Utilities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Exception.Core;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

public abstract class BaseProxyErrorFormatter : IProxyErrorFormatter
{
    public abstract bool Supports(RouteProfile profile);

    public ProxyErrorResponse Format(Exception exception, int statusCode)
    {
        // AI-Relay 内部异常直接返回原始消息。
        // 不注入 Overloaded 标记：openclaw 的 profile rotation 对单一代理端点无效，
        // 注入反而会触发 2-3 次多余重试。内部重试已由 AI-Relay 自身完成。
        return BuildResponse(statusCode, exception.Message);
    }

    public virtual ProxyErrorResponse Normalize(int statusCode, string? upstreamBody)
    {
        var message = ErrorMessageExtractor.TryExtractMessage(upstreamBody) ?? "Service Temporarily Unavailable";
        return BuildResponse(statusCode, message);
    }

    protected abstract ProxyErrorResponse BuildResponse(int statusCode, string message);
}

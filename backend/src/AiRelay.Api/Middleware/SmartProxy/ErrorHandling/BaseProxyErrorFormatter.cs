using AiRelay.Domain.Shared.Utilities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Exception.Core;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

public abstract class BaseProxyErrorFormatter : IProxyErrorFormatter
{
    public abstract bool Supports(RouteProfile profile);

    public ProxyErrorResponse Format(Exception exception, int statusCode)
    {
        var message = exception.Message;
        if (exception is ServiceUnavailableException)
        {
            message = EnsureOverloaded(message);
        }

        return BuildResponse(statusCode, $"代理网关异常被拦截: {message}");
    }

    public virtual ProxyErrorResponse Normalize(int statusCode, string? upstreamBody)
    {
        var message = ErrorMessageExtractor.TryExtractMessage(upstreamBody) ?? "Service Temporarily Unavailable";
        if (statusCode is 429 or 503)
        {
            message = EnsureOverloaded(message);
        }

        return BuildResponse(statusCode, message);
    }

    protected abstract ProxyErrorResponse BuildResponse(int statusCode, string message);

    private static string EnsureOverloaded(string message) =>
        message.Contains("(Overloaded)", StringComparison.Ordinal)
            ? message
            : $"Service Unavailable (Overloaded): {message}";
}

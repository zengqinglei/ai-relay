using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Exception.Core;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

public abstract class BaseProxyErrorFormatter : IProxyErrorFormatter
{
    public abstract bool Supports(RouteProfile profile);

    public ProxyErrorResponse Format(Exception exception)
    {
        var statusCode = ResolveStatusCode(exception);
        return BuildResponse(statusCode, exception.Message);
    }

    protected abstract ProxyErrorResponse BuildResponse(int statusCode, string message);

    protected static int ResolveStatusCode(Exception exception) => exception switch
    {
        BusinessException biz => int.Parse(biz.Code.ToString()[..3]),
        OperationCanceledException { InnerException: TimeoutException } => 503,
        TimeoutException => 503,
        HttpRequestException => 503,
        _ => 500
    };
}

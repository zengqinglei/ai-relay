using AiRelay.Application.ModelRoutes.Handlers;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Exception.Core;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

internal sealed class RouteTerminalFormattedException(
    int statusCode,
    Exception? originalException = null,
    string? errorBody = null)
    : Exception(errorBody ?? originalException?.Message ?? "未知错误", originalException)
{
    public int StatusCode { get; } = statusCode;
    public Exception? OriginalException { get; } = originalException;
    public string? ErrorBody { get; } = errorBody;
}

public abstract class BaseProxyErrorFormatter(RouteTerminalErrorFormatter routeTerminalErrorFormatter) : IProxyErrorFormatter
{
    public abstract bool Supports(RouteProfile profile);

    public ProxyErrorResponse Format(Exception exception)
    {
        var statusCode = ResolveStatusCode(exception);
        var message = exception is RouteTerminalFormattedException routeTerminal
            ? routeTerminalErrorFormatter.BuildMessage(
                RouteTerminalError.InternalException(
                    routeTerminal.OriginalException ?? routeTerminal,
                    routeTerminal.StatusCode,
                    routeTerminal.ErrorBody ?? routeTerminal.OriginalException?.Message ?? routeTerminal.Message))
            : routeTerminalErrorFormatter.BuildMessage(
                RouteTerminalError.InternalException(exception, statusCode, exception.Message));

        return BuildResponse(statusCode, message);
    }

    public virtual ProxyErrorResponse Normalize(int statusCode, string? upstreamBody)
    {
        var originalMessage = routeTerminalErrorFormatter.BuildMessage(
            RouteTerminalError.UpstreamNormalized(statusCode, upstreamBody));

        return BuildResponse(statusCode, originalMessage);
    }

    protected abstract ProxyErrorResponse BuildResponse(int statusCode, string message);

    protected static int ResolveStatusCode(Exception exception) => exception switch
    {
        RouteTerminalFormattedException routeTerminal => routeTerminal.StatusCode,
        BusinessException biz => int.Parse(biz.Code.ToString()[..3]),
        OperationCanceledException { InnerException: TimeoutException } => 503,
        TimeoutException => 503,
        HttpRequestException => 503,
        _ => 500
    };
}

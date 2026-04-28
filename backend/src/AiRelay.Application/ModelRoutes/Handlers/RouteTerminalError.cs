namespace AiRelay.Application.ModelRoutes.Handlers;

public enum RouteTerminalErrorKind
{
    UpstreamNormalized,
    InternalException
}

public sealed record RouteTerminalError(
    RouteTerminalErrorKind Kind,
    int StatusCode,
    string? ErrorBody = null,
    Exception? Exception = null)
{
    public static RouteTerminalError UpstreamNormalized(int statusCode, string? errorBody) =>
        new(RouteTerminalErrorKind.UpstreamNormalized, statusCode, errorBody);

    public static RouteTerminalError InternalException(Exception exception, int statusCode, string? errorBody = null) =>
        new(RouteTerminalErrorKind.InternalException, statusCode, errorBody, exception);
}

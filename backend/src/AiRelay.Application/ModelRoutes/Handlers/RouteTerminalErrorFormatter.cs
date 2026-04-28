using System.Text.Json;

namespace AiRelay.Application.ModelRoutes.Handlers;

public class RouteTerminalErrorFormatter
{
    public string BuildMessage(RouteTerminalError error)
    {
        var message = error.Kind switch
        {
            RouteTerminalErrorKind.UpstreamNormalized => NormalizeUpstreamMessage(error.StatusCode, error.ErrorBody),
            _ => error.Exception?.Message ?? error.ErrorBody ?? "未知错误"
        };

        return error.Kind == RouteTerminalErrorKind.UpstreamNormalized
            ? message
            : $"代理网关异常被拦截: {message}";
    }

    private static string NormalizeUpstreamMessage(int statusCode, string? upstreamBody)
    {
        var originalMessage = TryExtractMessage(upstreamBody) ?? "Service Temporarily Unavailable";
        if (statusCode is 429 or 503)
        {
            originalMessage = EnrichMessage(originalMessage);
        }

        return originalMessage;
    }

    private static string EnrichMessage(string message) =>
        message.Contains("(Overloaded)", StringComparison.Ordinal)
            ? message
            : $"Service Unavailable (Overloaded): {message}";

    private static string? TryExtractMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var nestedMessage))
            {
                return nestedMessage.GetString();
            }

            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : body;
        }
        catch
        {
            return body;
        }
    }
}

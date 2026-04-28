using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiRelay.Application.ModelRoutes.Handlers;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Api.Middleware.SmartProxy.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace AiRelay.Api.Middleware.SmartProxy.Handlers;

public class ProxyRouteResponseHandler(
    HttpContext context,
    ProxyErrorFormatterFactory errorFormatterFactory,
    RouteProfile routeProfile) : IRouteResponseHandler
{
    public bool HasResponseStarted => context.Response.HasStarted;

    public bool ShouldHandle(StreamEvent streamEvent, byte[]? bytesToForward)
        => bytesToForward != null;

    public Task OnHeadersReadyAsync(int statusCode, Dictionary<string, IEnumerable<string>> headers, CancellationToken ct)
    {
        context.Response.StatusCode = statusCode;
        foreach (var header in headers)
        {
            if (!IsHopByHopHeader(header.Key))
            {
                context.Response.Headers.Append(header.Key, new StringValues(header.Value.ToArray()));
            }
        }
        return Task.CompletedTask;
    }

    public async Task OnDataAsync(StreamEvent streamEvent, byte[]? originalBytes, CancellationToken ct)
    {
        if (originalBytes != null)
        {
            await context.Response.Body.WriteAsync(originalBytes, ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }

    public async Task OnTerminalErrorAsync(RouteTerminalError error, CancellationToken ct)
    {
        var formatter = errorFormatterFactory.GetFormatter(routeProfile);

        if (error.Kind == RouteTerminalErrorKind.UpstreamNormalized)
        {
            var normalized = formatter.Normalize(error.StatusCode, error.ErrorBody);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = normalized.StatusCode;
                await context.Response.WriteAsync(normalized.Payload, ct);
            }
        }
        else
        {
            var errorResponse = formatter.Format(new RouteTerminalFormattedException(
                error.StatusCode,
                error.Exception,
                error.ErrorBody));
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = errorResponse.StatusCode;
                context.Response.ContentType = errorResponse.ContentType;
                await context.Response.WriteAsync(errorResponse.Payload, Encoding.UTF8, ct);
            }
        }
    }

    public void AbortConnection()
    {
        context.Abort();
    }

    private static bool IsHopByHopHeader(string headerName) => headerName.ToLowerInvariant() switch
    {
        "connection" or "keep-alive" or "transfer-encoding" or "te" or "trailer" or
        "proxy-authorization" or "proxy-authenticate" or "upgrade" or "expect" or "proxy-connection" => true,
        _ => false
    };
}

using AiRelay.Api.Options;

namespace AiRelay.Api.Extensions;

public static class SpaProxyApplicationBuilderExtensions
{
    private const string HttpClientName = "SpaProxy";

    /// <summary>
    /// HTTP/2 禁止的 hop-by-hop 头（RFC 7540 §8.1.2.2）
    /// Kestrel 在 HTTP/2 响应中自动剥离这些头时会产生大量 WRN 日志
    /// </summary>
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Transfer-Encoding",
        "Keep-Alive",
        "Upgrade",
        "Proxy-Connection"
    };

    public static IServiceCollection AddAiRelaySpaProxy(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName);
        return services;
    }

    public static IEndpointRouteBuilder MapAiRelaySpaFallback(this WebApplication app)
    {
        var options = app.Configuration.GetSection(SpaProxyOptions.SectionName).Get<SpaProxyOptions>() ?? new SpaProxyOptions();
        if (options.Enabled && !string.IsNullOrWhiteSpace(options.Target))
        {
            var target = new Uri(options.Target);
            app.Map("{**path}", context => ProxySpaDevServerAsync(context, target))
               .WithDisplayName("SpaProxyFallback");
        }
        else
        {
            app.MapFallbackToFile("index.html");
        }

        return app;
    }

    private static async Task ProxySpaDevServerAsync(HttpContext context, Uri target)
    {
        // WebSocket CONNECT 等非标准方法无法通过 HttpClient 代理
        // 优雅降级：返回 404 而非抛异常导致 500/503
        if (HttpMethods.IsConnect(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var requestUri = new UriBuilder(target)
        {
            Path = context.Request.Path,
            Query = context.Request.QueryString.Value
        }.Uri;

        using var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), requestUri);
        foreach (var header in context.Request.Headers)
        {
            if (!string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase) &&
                !HopByHopHeaders.Contains(header.Key))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
            foreach (var header in context.Request.Headers)
            {
                requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        requestMessage.Headers.Host = target.IsDefaultPort ? target.Host : $"{target.Host}:{target.Port}";

        var httpClient = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
        using var responseMessage = await httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)responseMessage.StatusCode;

        // 复制响应头，过滤 hop-by-hop 头以避免 HTTP/2 协议冲突警告
        foreach (var header in responseMessage.Headers)
        {
            if (!HopByHopHeaders.Contains(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        foreach (var header in responseMessage.Content.Headers)
        {
            if (!HopByHopHeaders.Contains(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}

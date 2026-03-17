using Leistd.Tracing.HttpClient.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.Tracing.HttpClient;

public static class DependencyInjection
{
    /// <summary>
    /// 为 HttpClient 添加 TraceId 转发能力
    /// </summary>
    public static IHttpClientBuilder AddCorrelationIdForwarding(this IHttpClientBuilder builder)
    {
        builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
        builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
        return builder;
    }
}

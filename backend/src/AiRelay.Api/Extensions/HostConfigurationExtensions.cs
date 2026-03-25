using Leistd.Tracing.AspNetCore;
using Leistd.Tracing.HttpClient;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;

namespace AiRelay.Api.Extensions;

public static class HostConfigurationExtensions
{
    /// <summary>
    /// 配置 Web 服务器选项 (Kestrel, IIS, Form)
    /// </summary>
    public static IServiceCollection AddAiRelayWebServer(this IServiceCollection services)
    {
        // 限制请求体大小为 500MB（针对 Gemini 1.5 Pro 视频上传优化，防止 OOM 但允许大文件流式传输）
        const long MaxRequestBodySize = 524288000; // 500MB

        services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = MaxRequestBodySize;
        });

        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = MaxRequestBodySize;
        });

        services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartBodyLengthLimit = MaxRequestBodySize;
            options.MultipartHeadersLengthLimit = int.MaxValue;
        });

        return services;
    }

    /// <summary>
    /// 配置基础设施服务 (Serilog, CorrelationId, HttpClient Defaults)
    /// </summary>
    public static WebApplicationBuilder AddAiRelayInfrastructure(this WebApplicationBuilder builder)
    {
        // Serilog
        builder.Services.AddSerilog((services, lc) =>
        {
            lc.ReadFrom.Configuration(builder.Configuration)
              .Enrich.FromLogContext();
        });

        // CorrelationId
        builder.Services.AddCorrelationId(builder.Configuration);

        // HttpClient Defaults
        builder.Services.ConfigureHttpClientDefaults(httpBuilder =>
        {
            // httpBuilder.AddCorrelationIdForwarding(); 避免转发给上游的请求携带
            httpBuilder.ConfigureHttpClient(client =>
            {
                // 1. SSE 流式响应使用 Infinite 超时，实际生命周期由 CancellationToken 严格控制
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

            httpBuilder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // 2. 解决 DNS 刷新问题 (5分钟过期，不影响活跃连接)
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),

                // 3. 握手超时：弱网下防黑洞死等（包含 TCP + SSL 握手总时长）
                ConnectTimeout = TimeSpan.FromSeconds(60),

                // 4. Keep-Alive 设置：防止弱网下发生”静默断网”导致无限等待
                // 适用于 HTTP/2 的 Ping 帧发送间隔
                KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                // 如果 60 秒内没有收到 Ping 的回复，则强制断开连接
                KeepAlivePingTimeout = TimeSpan.FromMinutes(1),
                // 无论请求是否处于活跃状态，都发送 Ping (适用于长时间无数据传输的 SSE)
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always
            });
        });

        return builder;
    }
}

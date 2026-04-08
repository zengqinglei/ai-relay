using Leistd.Tracing.AspNetCore;
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



        return builder;
    }
}

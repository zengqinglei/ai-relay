using Leistd.Tracing.AspNetCore.Middlewares;
using Leistd.Tracing.Core;
using Leistd.Tracing.Core.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.Tracing.AspNetCore;

public static class DependencyInjection
{
    public static IServiceCollection AddCorrelationId(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册核心服务
        services.AddCorrelationIdCore(configuration);
        return services;
    }

    public static IServiceCollection AddCorrelationId(
        this IServiceCollection services,
        Action<CorrelationIdOptions> configureOptions)
    {
        services.AddCorrelationIdCore(configureOptions);
        return services;
    }

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Leistd.Exception.AspNetCore.Handlers;
using Leistd.Exception.AspNetCore.Options;

namespace Leistd.Exception.AspNetCore;

public static class DependencyInjection
{
    public static IServiceCollection AddGlobalExceptionHandler(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册 ProblemDetails 服务
        services.AddProblemDetails();

        services.Configure<GlobalExceptionOptions>(
            configuration.GetSection("Leistd:GlobalException"));

        services.AddExceptionHandler<BusinessExceptionHandler>();

        return services;
    }

    public static IServiceCollection AddGlobalExceptionHandler(
        this IServiceCollection services,
        Action<GlobalExceptionOptions> configureOptions)
    {
        // 注册 ProblemDetails 服务
        services.AddProblemDetails();

        services.Configure(configureOptions);
        services.AddExceptionHandler<BusinessExceptionHandler>();

        return services;
    }

    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            AllowStatusCode404Response = true,
            ExceptionHandler = null,
            // .NET 10: 客户端主动断开（OperationCanceledException）不记录 diagnostics/ERR
            SuppressDiagnosticsCallback = ctx =>
                ctx.Exception is OperationCanceledException
                    && ctx.HttpContext.RequestAborted.IsCancellationRequested
        });
    }
}

using Leistd.Security.AspNetCore.Claims;
using Leistd.Security.Claims;
using Leistd.Security.Clients;
using Leistd.Security.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.Security.AspNetCore;

/// <summary>
/// Leistd Security 扩展方法
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 Leistd Security 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddLeistdSecurity(this IServiceCollection services)
    {
        // HTTP 上下文访问器
        services.AddHttpContextAccessor();

        // 核心服务
        services.AddSingleton<ICurrentPrincipalAccessor, HttpContextCurrentPrincipalAccessor>();
        services.AddTransient<ICurrentUser, CurrentUser>();
        services.AddTransient<ICurrentClient, CurrentClient>();

        return services;
    }

    /// <summary>
    /// 使用 Leistd Security 中间件
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>应用程序构建器</returns>
    public static IApplicationBuilder UseLeistdSecurity(this IApplicationBuilder app)
    {
        // 预留中间件扩展点
        return app;
    }
}

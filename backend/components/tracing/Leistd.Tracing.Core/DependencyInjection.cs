using Castle.DynamicProxy;
using Leistd.DependencyInjection;
using Leistd.Tracing.Core.Attributes;
using Leistd.Tracing.Core.Interceptors;
using Leistd.Tracing.Core.Options;
using Leistd.Tracing.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leistd.Tracing.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCorrelationIdCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CorrelationIdOptions>(configuration.GetSection("Leistd:CorrelationId"));
        return AddCorrelationIdCore(services);
    }

    public static IServiceCollection AddCorrelationIdCore(
        this IServiceCollection services,
        Action<CorrelationIdOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return AddCorrelationIdCore(services);
    }

    private static IServiceCollection AddCorrelationIdCore(IServiceCollection services)
    {
        // 注册核心服务
        services.TryAddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();

        // 注册拦截器
        services.TryAddTransient<CorrelationIdInterceptor>();
        services.TryAddSingleton<IProxyGenerator, ProxyGenerator>();

        // 注册服务回调，扫描带有 [CorrelationId] 特性的服务
        services.OnServiceRegistered(context =>
        {
            if (ShouldIntercept(context.ImplementationType))
            {
                context.Interceptors.Add(typeof(CorrelationIdInterceptor));
            }
        });

        return services;
    }

    private static bool ShouldIntercept(Type implementationType)
    {
        if (implementationType == null) return false;

        // 检查类特性
        if (implementationType.GetCustomAttributes(typeof(CorrelationIdAttribute), true).Any())
            return true;

        // 检查方法特性
        return implementationType.GetMethods()
            .Any(m => m.GetCustomAttributes(typeof(CorrelationIdAttribute), true).Any());
    }
}

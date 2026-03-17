using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Leistd.DependencyInjection;
using Leistd.EventBus.Core.EventHandler;
using Leistd.UnitOfWork.Core.Uow;
using Leistd.UnitOfWork.Core.Options;
using Leistd.UnitOfWork.Core.Interceptor;
using Leistd.UnitOfWork.Core.Attributes;
using Leistd.UnitOfWork.Core.Events;

namespace Leistd.UnitOfWork.Core;

/// <summary>
/// UnitOfWork 组件依赖注入配置
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 UnitOfWork 核心服务
    /// </summary>
    public static IServiceCollection AddUnitOfWork(
        this IServiceCollection services,
        Action<UnitOfWorkOptions>? configureOptions = null)
    {
        // 注册 Castle DynamicProxy（如果未注册）
        services.TryAddSingleton<IProxyGenerator, ProxyGenerator>();

        // 注册工作单元选项
        var options = new UnitOfWorkOptions();
        configureOptions?.Invoke(options);
        services.TryAddSingleton(options);

        // 注册工作单元核心服务
        services.TryAddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.TryAddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.TryAddTransient<IUnitOfWork, Uow.UnitOfWork>();
        services.TryAddTransient<UnitOfWorkInterceptor>();
        services.TryAddTransient<UnitOfWorkEventHandlerInterceptor>();

        // 使用 OnServiceRegistered 注册拦截器回调
        // 回调将在构建 IServiceProvider 时自动执行

        // 为带 [UnitOfWork] 特性的服务添加拦截器
        services.OnServiceRegistered(context =>
        {
            if (ShouldInterceptUnitOfWork(context.ImplementationType))
            {
                context.Interceptors.Add(typeof(UnitOfWorkInterceptor));
            }
        });

        // 为带 [UnitOfWorkEventHandler] 特性的事件处理器添加拦截器
        services.OnServiceRegistered(context =>
        {
            if (ShouldInterceptEventHandler(context))
            {
                context.Interceptors.Add(typeof(UnitOfWorkEventHandlerInterceptor));
            }
        });

        return services;
    }

    /// <summary>
    /// 判断是否需要拦截 UnitOfWork
    /// </summary>
    private static bool ShouldInterceptUnitOfWork(Type implementationType)
    {
        if (implementationType == null) return false;

        // 检查类上的特性
        if (implementationType.GetCustomAttributes(typeof(UnitOfWorkAttribute), true).Any())
            return true;

        // 检查方法上的特性
        return implementationType.GetMethods()
            .Any(m => m.GetCustomAttributes(typeof(UnitOfWorkAttribute), true).Any());
    }

    /// <summary>
    /// 判断是否需要拦截事件处理器
    /// </summary>
    private static bool ShouldInterceptEventHandler(IOnServiceRegistredContext context)
    {
        // 检查是否是 IEventHandler<> 接口
        if (!context.ServiceType.IsGenericType)
            return false;

        var genericTypeDef = context.ServiceType.GetGenericTypeDefinition();
        if (genericTypeDef != typeof(IEventHandler<>))
            return false;

        // 检查是否有 [UnitOfWorkEventHandler] 特性
        return context.ImplementationType.GetCustomAttributes(typeof(UnitOfWorkEventHandlerAttribute), true).Any();
    }
}

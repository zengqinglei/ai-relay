using Microsoft.Extensions.DependencyInjection;

namespace Leistd.DependencyInjection;

/// <summary>
/// IServiceCollection 服务注册扩展方法
/// </summary>
public static class ServiceCollectionRegistrationExtensions
{
    /// <summary>
    /// 注册服务注册回调（类似 ABP 的 OnRegistered）
    /// 回调将在构建 IServiceProvider 时自动执行
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="registrationAction">注册回调</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection OnServiceRegistered(
        this IServiceCollection services,
        Action<IOnServiceRegistredContext> registrationAction)
    {
        GetOrCreateRegistrationActionList(services).Add(registrationAction);
        return services;
    }

    /// <summary>
    /// 获取已注册的回调列表
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>回调列表</returns>
    public static ServiceRegistrationActionList GetRegistrationActionList(this IServiceCollection services)
    {
        return GetOrCreateRegistrationActionList(services);
    }

    /// <summary>
    /// 获取或创建服务注册回调列表
    /// </summary>
    private static ServiceRegistrationActionList GetOrCreateRegistrationActionList(IServiceCollection services)
    {
        // 查找已注册的 ServiceRegistrationActionList
        var actionList = services
            .FirstOrDefault(d => d.ServiceType == typeof(ServiceRegistrationActionList))
            ?.ImplementationInstance as ServiceRegistrationActionList;

        if (actionList == null)
        {
            // 创建新的回调列表
            actionList = new ServiceRegistrationActionList();

            // 注册为单例
            services.AddSingleton(actionList);
        }

        return actionList;
    }
}

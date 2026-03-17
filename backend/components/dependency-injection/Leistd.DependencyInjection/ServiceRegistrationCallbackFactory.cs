using Castle.DynamicProxy;
using Leistd.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.DependencyInjection;

/// <summary>
/// 服务注册回调工厂
/// 在构建 IServiceProvider 时自动执行所有已注册的回调
/// </summary>
public class ServiceRegistrationCallbackFactory : IServiceProviderFactory<IServiceCollection>
{
    /// <summary>
    /// 创建服务集合构建器
    /// </summary>
    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// 构建 IServiceProvider
    /// 在构建前执行所有已注册的回调
    /// </summary>
    public IServiceProvider CreateServiceProvider(IServiceCollection services)
    {
        // 获取所有已注册的回调
        var actionList = services.GetRegistrationActionList();

        if (actionList.Any())
        {
            ProcessServiceRegistrations(services, actionList);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 处理服务注册
    /// </summary>
    private void ProcessServiceRegistrations(
        IServiceCollection services,
        ServiceRegistrationActionList actionList)
    {
        // 创建一个副本，避免在遍历时修改集合
        var descriptors = services.ToList();

        foreach (var descriptor in descriptors)
        {
            // 获取实现类型
            var implementationType = descriptor.ImplementationType
                ?? descriptor.ImplementationInstance?.GetType();

            // 跳过不支持的类型
            if (implementationType == null ||
                implementationType.IsAbstract ||
                implementationType.IsInterface)
            {
                continue;
            }

            // 创建上下文
            var context = new OnServiceRegistredContext(
                descriptor.ServiceType,
                implementationType);

            // 执行所有回调
            foreach (var action in actionList)
            {
                action.Invoke(context);
            }

            // 如果回调添加了拦截器，应用装饰器
            if (context.Interceptors.Any())
            {
                ApplyInterceptors(services, descriptor, context.Interceptors);
            }
        }
    }

    /// <summary>
    /// 应用拦截器装饰器
    /// </summary>
    private void ApplyInterceptors(
        IServiceCollection services,
        ServiceDescriptor descriptor,
        List<Type> interceptorTypes)
    {
        // 找到原始服务的索引
        var index = services.IndexOf(descriptor);
        if (index < 0) return;

        // 替换为装饰后的服务
        services[index] = ServiceDescriptor.Describe(
            descriptor.ServiceType,
            sp =>
            {
                // 创建原始实例
                object instance = CreateOriginalInstance(descriptor, sp);

                // 应用拦截器
                var proxyGenerator = sp.GetRequiredService<IProxyGenerator>();

                // 创建拦截器实例并按 Order 排序
                var interceptors = interceptorTypes
                    .Select(t => sp.GetRequiredService(t))
                    .Cast<IInterceptor>()
                    .OrderBy(interceptor =>
                        interceptor is BaseAsyncInterceptor asyncInterceptor
                            ? asyncInterceptor.Order
                            : 0)
                    .ToArray();

                // 生成代理
                if (descriptor.ServiceType.IsInterface)
                {
                    return proxyGenerator.CreateInterfaceProxyWithTarget(
                        descriptor.ServiceType,
                        instance,
                        interceptors);
                }
                else
                {
                    return proxyGenerator.CreateClassProxyWithTarget(
                        descriptor.ServiceType,
                        instance,
                        interceptors);
                }
            },
            descriptor.Lifetime);
    }

    /// <summary>
    /// 创建原始实例
    /// </summary>
    private object CreateOriginalInstance(ServiceDescriptor descriptor, IServiceProvider sp)
    {
        if (descriptor.ImplementationInstance != null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory != null)
        {
            return descriptor.ImplementationFactory(sp);
        }

        if (descriptor.ImplementationType != null)
        {
            return ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

        throw new InvalidOperationException($"Cannot create instance for {descriptor.ServiceType}");
    }
}

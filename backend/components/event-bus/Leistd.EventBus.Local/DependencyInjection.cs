using Leistd.EventBus.Core.EventBus;
using Leistd.EventBus.Local.EventBus;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.EventBus.Local;

public static class DependencyInjection
{
    /// <summary>
    /// 注册本地事件总线 (Singleton)
    /// 适用于 Web、Console、BackgroundService 等多种应用场景
    /// </summary>
    public static IServiceCollection AddLocalEventBus(this IServiceCollection services)
    {
        // 注册为 Singleton，全局共享一个实例
        // 内部通过 IServiceScopeFactory 创建独立的 scope 来解析 Scoped 的 EventHandler
        services.AddSingleton<LocalEventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<LocalEventBus>());
        services.AddSingleton<ILocalEventBus>(sp => sp.GetRequiredService<LocalEventBus>());
        return services;
    }
}
using Leistd.Lock.Core;
using Leistd.Lock.Memory.HostedServices;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.Lock.Memory;

public static class DependencyInjection
{
    /// <summary>
    /// 注册内存本地锁（Singleton + IHostedService）
    /// 适用于单机部署、集成测试场景
    /// </summary>
    public static IServiceCollection AddMemoryLocalLock(this IServiceCollection services)
    {
        services.AddSingleton<MemoryLocalLock>();
        services.AddSingleton<ILocalLock, MemoryLocalLock>();
        services.AddSingleton<ILock, MemoryLocalLock>();
        services.AddHostedService<MemoryLockCleanupHostedService>();
        return services;
    }
}

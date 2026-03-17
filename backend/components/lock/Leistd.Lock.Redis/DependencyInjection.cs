using Leistd.Lock.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Leistd.Lock.Redis;

public static class DependencyInjection
{
    /// <summary>
    /// 注册 Redis 分布式锁
    /// </summary>
    public static IServiceCollection AddRedisDistributedLock(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();
        services.AddSingleton<ILock, RedisDistributedLock>();
        return services;
    }
}

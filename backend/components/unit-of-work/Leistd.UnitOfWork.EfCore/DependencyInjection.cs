using Leistd.UnitOfWork.EfCore.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leistd.UnitOfWork.EfCore;

/// <summary>
/// UnitOfWork EF Core 扩展依赖注入配置
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 EF Core UnitOfWork 支持
    /// </summary>
    public static IServiceCollection AddUnitOfWorkEfCore(
        this IServiceCollection services)
    {
        // 注册 EF Core DbContext Provider（泛型服务）
        services.TryAddScoped(typeof(IDbContextProvider<>), typeof(DbContextProvider<>));

        return services;
    }
}

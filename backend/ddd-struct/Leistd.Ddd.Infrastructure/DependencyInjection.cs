using System.Reflection;
using Leistd.DependencyInjection;
using Leistd.Ddd.Domain.Auditing;
using Leistd.Ddd.Domain.DataFilters;
using Leistd.Ddd.Domain.Entities;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Ddd.Infrastructure.Auditing;
using Leistd.Timing;
using Leistd.UnitOfWork.Core;
using Leistd.UnitOfWork.Core.Options;
using Leistd.UnitOfWork.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;

namespace Leistd.Ddd.Infrastructure;

/// <summary>
/// DDD Infrastructure 层依赖注入配置
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 DDD Infrastructure 基础服务（UnitOfWork + 自动 DbContext 仓储注册）
    /// </summary>
    public static IServiceCollection AddDddInfrastructure(
        this IServiceCollection services,
        Action<UnitOfWorkOptions>? configureUnitOfWork = null)
    {
        // 注册 IQueryable 异步执行器
        services.AddSingleton<IQueryableAsyncExecuter, EfCoreQueryableAsyncExecuter>();

        // 注册时钟服务
        services.AddSingleton<IClock, UtcClockProvider>();

        // 注册审计属性设置器
        services.AddScoped<IAuditPropertySetter, AuditPropertySetter>();

        // 注册 DataFilter 服务
        services.AddSingleton<IDataFilter, DataFilter>(); // 非泛型版本，单例
        services.AddScoped(typeof(IDataFilter<>), typeof(DataFilter<>)); // 泛型版本，作用域

        // 注册 UnitOfWork 组件（包含拦截器基础设施）
        services.AddUnitOfWork(configureUnitOfWork);

        // 注册 EF Core 支持
        services.AddUnitOfWorkEfCore();

        // 使用 OnServiceRegistered 自动为所有 DbContext 注册仓储
        // 回调将在构建 IServiceProvider 时自动执行
        services.OnServiceRegistered(context =>
        {
            if (typeof(DbContext).IsAssignableFrom(context.ImplementationType) &&
                !context.ImplementationType.IsAbstract &&
                context.ImplementationType != typeof(DbContext))
            {
                RegisterRepositoriesForDbContext(services, context.ImplementationType);
            }
        });

        return services;
    }

    /// <summary>
    /// 为指定的 DbContext 类型注册仓储
    /// </summary>
    private static void RegisterRepositoriesForDbContext(IServiceCollection services, Type dbContextType)
    {
        // 扫描 DbContext 的所有 DbSet<> 属性
        var entityTypes = dbContextType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .Where(t => typeof(IEntity).IsAssignableFrom(t));

        foreach (var entityType in entityTypes)
        {
            // 检测主键类型
            var keyType = FindPrimaryKeyType(entityType);

            // 注册 IRepository<TEntity>
            var repoInterfaceNoKey = typeof(IRepository<>).MakeGenericType(entityType);
            var repoImplNoKey = typeof(EfCoreRepository<,>).MakeGenericType(dbContextType, entityType);
            services.AddScoped(repoInterfaceNoKey, repoImplNoKey);

            if (keyType != null)
            {
                // 有主键：注册 IRepository<TEntity, TKey>
                var repoInterface = typeof(IRepository<,>).MakeGenericType(entityType, keyType);
                var repoImpl = typeof(EfCoreRepository<,,>).MakeGenericType(dbContextType, entityType, keyType);
                services.AddScoped(repoInterface, repoImpl);
            }
        }
    }

    /// <summary>
    /// 查找实体的主键类型
    /// </summary>
    private static Type? FindPrimaryKeyType(Type entityType)
    {
        // 查找 IEntity<TKey> 接口获取主键类型
        var entityInterface = entityType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IEntity<>));
        return entityInterface?.GetGenericArguments()[0];
    }
}

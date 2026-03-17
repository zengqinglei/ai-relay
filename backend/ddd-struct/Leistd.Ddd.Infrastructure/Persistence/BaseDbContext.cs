using Leistd.Ddd.Domain.DataFilters;
using Leistd.Ddd.Domain.Entities.Auditing;
using Leistd.Ddd.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.Ddd.Infrastructure.Persistence;

/// <summary>
/// 基础 DbContext
/// </summary>
/// <remarks>
/// 审计字段和本地事件的处理已迁移至 SaveChangesInterceptor：
/// - AuditSaveChangesInterceptor：处理审计字段自动填充
/// - LocalEventSaveChangesInterceptor：处理本地事件收集和发布
/// 这是 EF Core 官方推荐的最佳实践
/// </remarks>
public abstract class BaseDbContext : DbContext
{
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// 软删除过滤器是否启用
    /// </summary>
    protected virtual bool IsSoftDeleteFilterEnabled =>
        _serviceProvider?.GetService<IDataFilter>()?.IsEnabled<ISoftDelete>() ?? true;

    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    protected BaseDbContext(
        DbContextOptions options,
        IServiceProvider? serviceProvider) : base(options)
    {
        _serviceProvider = serviceProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ 使用扩展方法配置全局软删除过滤器（无反射，性能更好）
        // 表达式逻辑：!IsSoftDeleteFilterEnabled || !e.IsDeleted
        // - 如果过滤器被禁用，则返回 true（不过滤，显示所有数据）
        // - 如果过滤器启用，则只返回未删除的数据
        modelBuilder.ApplyGlobalFilters<ISoftDelete>(e =>
            !IsSoftDeleteFilterEnabled ||
            !EF.Property<bool>(e, nameof(ISoftDelete.IsDeleted)));
    }
}
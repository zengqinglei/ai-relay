using System.Linq.Expressions;
using Leistd.Ddd.Domain.Entities.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;

namespace Leistd.Ddd.Infrastructure.Persistence.Extensions;

/// <summary>
/// ModelBuilder 扩展方法
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// 按 ABP 约定配置实体基类属性（审计字段长度约束）
    /// 应在每个实体的 Fluent API 配置块中优先调用
    /// </summary>
    public static void ConfigureByConvention<TEntity>(this EntityTypeBuilder<TEntity> b)
        where TEntity : class
    {
        var type = typeof(TEntity);

        if (typeof(ICreationAuditedObject).IsAssignableFrom(type))
        {
            b.Property(nameof(ICreationAuditedObject.CreatorId)).HasMaxLength(64);
        }

        if (typeof(IModificationAuditedObject).IsAssignableFrom(type))
        {
            b.Property(nameof(IModificationAuditedObject.LastModifierId)).HasMaxLength(64);
        }

        if (typeof(IDeletionAuditedObject).IsAssignableFrom(type))
        {
            b.Property(nameof(IDeletionAuditedObject.DeleterId)).HasMaxLength(64);
        }
    }


    /// <summary>
    /// 为所有实现指定接口的实体类型应用全局查询过滤器
    /// </summary>
    /// <typeparam name="TInterface">接口类型（如 ISoftDelete）</typeparam>
    /// <param name="modelBuilder">模型构建器</param>
    /// <param name="expression">过滤表达式</param>
    /// <remarks>
    /// 这是 EF Core 官方推荐的最佳实践，使用 ReplacingExpressionVisitor 替代反射。
    /// 相比使用 MakeGenericMethod + Invoke 的方式：
    /// - 性能更好：避免了反射调用开销
    /// - 代码更简洁：不需要单独的泛型方法
    /// - 可维护性更强：类型安全，编译时检查
    ///
    /// 参考：
    /// - https://learn.microsoft.com/en-us/ef/core/querying/filters
    /// - https://gist.github.com/haacked/febe9e88354fb2f4a4eb11ba88d64c24
    /// </remarks>
    public static void ApplyGlobalFilters<TInterface>(
        this ModelBuilder modelBuilder,
        Expression<Func<TInterface, bool>> expression)
    {
        // 获取所有实现指定接口的根实体类型（排除继承的实体）
        var entities = modelBuilder.Model
            .GetEntityTypes()
            .Where(t => t.BaseType == null) // 只处理根实体类型
            .Select(t => t.ClrType)
            .Where(t => typeof(TInterface).IsAssignableFrom(t));

        foreach (var entityType in entities)
        {
            // 创建新的参数表达式（实体类型）
            var newParam = Expression.Parameter(entityType);

            // ✅ 使用 ReplacingExpressionVisitor 替换表达式参数
            // 将 Expression<Func<TInterface, bool>> 转换为 Expression<Func<TEntity, bool>>
            var newBody = ReplacingExpressionVisitor.Replace(
                expression.Parameters.Single(),
                newParam,
                expression.Body);

            // 应用过滤器
            modelBuilder.Entity(entityType)
                .HasQueryFilter(Expression.Lambda(newBody, newParam));
        }
    }
}

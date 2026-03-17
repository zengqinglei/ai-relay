using Leistd.Ddd.Domain.Auditing;
using Leistd.Ddd.Domain.Entities.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Leistd.Ddd.Infrastructure.Auditing;

/// <summary>
/// 审计字段自动填充拦截器
/// </summary>
/// <remarks>
/// 使用 SaveChangesInterceptor 在保存前自动设置审计属性（创建时间、修改时间、删除时间等）
/// 这是 EF Core 官方推荐的最佳实践，相比 ChangeTracker 事件有以下优势：
/// 1. 时机更准确：在 SavingChanges 中处理，刚好在保存到数据库之前
/// 2. 支持异步：可以使用 async/await 进行异步操作
/// 3. 无需反射：直接通过 EntityEntry API 访问属性，避免 EF Core 代理类问题
/// 4. 更简洁：不需要订阅/取消订阅事件
/// </remarks>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IAuditPropertySetter _auditPropertySetter;

    public AuditSaveChangesInterceptor(IAuditPropertySetter auditPropertySetter)
    {
        _auditPropertySetter = auditPropertySetter;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return new ValueTask<InterceptionResult<int>>(result);
    }

    /// <summary>
    /// 根据实体状态更新审计字段
    /// </summary>
    private void UpdateAuditFields(DbContext? context)
    {
        if (context == null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    _auditPropertySetter.SetCreationProperties(entry);
                    break;

                case EntityState.Modified:
                    // 排除仅软删除标记的情况（避免重复设置修改审计）
                    if (entry.Entity is ISoftDelete { IsDeleted: true })
                        break;
                    _auditPropertySetter.SetModificationProperties(entry);
                    break;

                case EntityState.Deleted:
                    // 处理软删除：将物理删除转换为逻辑删除
                    if (entry.Entity is ISoftDelete)
                    {
                        entry.State = EntityState.Modified;
                        _auditPropertySetter.SetDeletionProperties(entry);
                    }
                    break;
            }
        }
    }
}

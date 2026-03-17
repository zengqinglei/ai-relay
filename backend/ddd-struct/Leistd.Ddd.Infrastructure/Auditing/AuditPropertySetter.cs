using Leistd.Ddd.Domain.Auditing;
using Leistd.Ddd.Domain.Entities.Auditing;
using Leistd.Security.Users;
using Leistd.Timing;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Leistd.Ddd.Infrastructure.Auditing;

/// <summary>
/// 审计属性设置器实现
/// </summary>
/// <remarks>
/// 使用 EF Core EntityEntry API 直接设置审计属性，避免反射带来的性能问题和代理类兼容性问题。
/// 基于 ABP 框架的审计属性设置逻辑，简化版本（无多租户、无 API Key 审计）
/// </remarks>
public class AuditPropertySetter(
    IClock clock,
    ICurrentUser currentUser) : IAuditPropertySetter
{
    /// <summary>
    /// 设置创建相关的审计属性
    /// </summary>
    public virtual void SetCreationProperties(object entityEntry)
    {
        if (entityEntry is not EntityEntry entry)
            return;

        SetCreationTime(entry);
        SetCreatorId(entry);
    }

    /// <summary>
    /// 设置修改相关的审计属性
    /// </summary>
    public virtual void SetModificationProperties(object entityEntry)
    {
        if (entityEntry is not EntityEntry entry)
            return;

        SetLastModificationTime(entry);
        SetLastModifierId(entry);
    }

    /// <summary>
    /// 设置删除相关的审计属性（软删除）
    /// </summary>
    public virtual void SetDeletionProperties(object entityEntry)
    {
        if (entityEntry is not EntityEntry entry)
            return;

        SetIsDeleted(entry);
        SetDeletionTime(entry);
        SetDeleterId(entry);
    }

    #region 受保护的虚方法（可扩展）

    /// <summary>
    /// 设置创建时间
    /// </summary>
    protected virtual void SetCreationTime(EntityEntry entry)
    {
        if (entry.Entity is not IHasCreationTime objectWithCreationTime)
            return;

        if (objectWithCreationTime.CreationTime != default)
            return;

        // ✅ 使用 EntityEntry API 直接设置属性，避免反射
        entry.Property(nameof(IHasCreationTime.CreationTime)).CurrentValue = clock.Normalize(clock.Now);
    }

    /// <summary>
    /// 设置创建者 ID
    /// </summary>
    protected virtual void SetCreatorId(EntityEntry entry)
    {
        if (!currentUser.Id.HasValue)
            return;

        if (entry.Entity is not ICreationAuditedObject creationAuditedObject)
            return;

        if (!string.IsNullOrEmpty(creationAuditedObject.CreatorId))
            return;

        entry.Property(nameof(ICreationAuditedObject.CreatorId)).CurrentValue = currentUser.Id.Value.ToString();
    }

    /// <summary>
    /// 设置最后修改时间
    /// </summary>
    protected virtual void SetLastModificationTime(EntityEntry entry)
    {
        if (entry.Entity is not IHasModificationTime)
            return;

        entry.Property(nameof(IHasModificationTime.LastModificationTime)).CurrentValue = clock.Normalize(clock.Now);
    }

    /// <summary>
    /// 设置最后修改者 ID
    /// </summary>
    protected virtual void SetLastModifierId(EntityEntry entry)
    {
        if (!currentUser.Id.HasValue)
            return;

        if (entry.Entity is not IModificationAuditedObject)
            return;

        entry.Property(nameof(IModificationAuditedObject.LastModifierId)).CurrentValue = currentUser.Id.Value.ToString();
    }

    /// <summary>
    /// 设置 IsDeleted 标志
    /// </summary>
    protected virtual void SetIsDeleted(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDelete softDelete)
            return;

        if (softDelete.IsDeleted)
            return;

        entry.Property(nameof(ISoftDelete.IsDeleted)).CurrentValue = true;
    }

    /// <summary>
    /// 设置删除时间
    /// </summary>
    protected virtual void SetDeletionTime(EntityEntry entry)
    {
        if (entry.Entity is not IHasDeletionTime objectWithDeletionTime)
            return;

        if (objectWithDeletionTime.DeletionTime.HasValue)
            return;

        entry.Property(nameof(IHasDeletionTime.DeletionTime)).CurrentValue = clock.Normalize(clock.Now);
    }

    /// <summary>
    /// 设置删除者 ID
    /// </summary>
    protected virtual void SetDeleterId(EntityEntry entry)
    {
        if (!currentUser.Id.HasValue)
            return;

        if (entry.Entity is not IDeletionAuditedObject deletionAuditedObject)
            return;

        if (!string.IsNullOrEmpty(deletionAuditedObject.DeleterId))
            return;

        entry.Property(nameof(IDeletionAuditedObject.DeleterId)).CurrentValue = currentUser.Id.Value.ToString();
    }

    #endregion
}
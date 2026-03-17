namespace Leistd.Ddd.Domain.Entities.Auditing;

/// <summary>
/// 删除审计实体基类（软删除，无主键）
/// </summary>
public abstract class DeletionAuditedEntity : ModificationAuditedEntity, IDeletionAuditedObject
{
    /// <inheritdoc />
    public virtual bool IsDeleted { get; protected set; }

    /// <inheritdoc />
    public virtual string? DeleterId { get; protected set; }

    /// <inheritdoc />
    public virtual DateTime? DeletionTime { get; protected set; }
}

/// <summary>
/// 删除审计实体基类（软删除，带主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class DeletionAuditedEntity<TKey> : ModificationAuditedEntity<TKey>, IDeletionAuditedObject
{
    /// <inheritdoc />
    public virtual bool IsDeleted { get; protected set; }

    /// <inheritdoc />
    public virtual string? DeleterId { get; protected set; }

    /// <inheritdoc />
    public virtual DateTime? DeletionTime { get; protected set; }
}

/// <summary>
/// 完整审计实体基类（创建+修改+删除，无主键）
/// </summary>
public abstract class FullAuditedEntity : DeletionAuditedEntity, IFullAuditedObject
{
}

/// <summary>
/// 完整审计实体基类（创建+修改+删除，带主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class FullAuditedEntity<TKey> : DeletionAuditedEntity<TKey>, IFullAuditedObject
{
}

namespace Leistd.Ddd.Domain.Entities.Auditing;

/// <summary>
/// 修改审计实体基类（无主键）
/// </summary>
public abstract class ModificationAuditedEntity : CreationAuditedEntity, IModificationAuditedObject
{
    /// <inheritdoc />
    public virtual string? LastModifierId { get; protected set; }

    /// <inheritdoc />
    public virtual DateTime? LastModificationTime { get; protected set; }
}

/// <summary>
/// 修改审计实体基类（带主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class ModificationAuditedEntity<TKey> : CreationAuditedEntity<TKey>, IModificationAuditedObject
{
    /// <inheritdoc />
    public virtual string? LastModifierId { get; protected set; }

    /// <inheritdoc />
    public virtual DateTime? LastModificationTime { get; protected set; }
}

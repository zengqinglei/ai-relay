namespace Leistd.Ddd.Domain.Entities.Auditing;

/// <summary>
/// 创建审计实体基类（无主键）
/// </summary>
public abstract class CreationAuditedEntity : Entity, ICreationAuditedObject
{
    /// <inheritdoc />
    public virtual string? CreatorId { get; protected set; }

    /// <inheritdoc />
    public virtual DateTime CreationTime { get; protected set; }
}

/// <summary>
/// 创建审计实体基类（带主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class CreationAuditedEntity<TKey> : Entity<TKey>, ICreationAuditedObject
{
    /// <inheritdoc />
    public virtual string? CreatorId { get; protected set; }

    /// <inheritdoc />
    public virtual DateTime CreationTime { get; protected set; }
}

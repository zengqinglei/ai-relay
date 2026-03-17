namespace Leistd.Ddd.Domain.Entities.Auditing;

/// <summary>
/// 有创建时间的对象
/// </summary>
public interface IHasCreationTime
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTime CreationTime { get; }
}

/// <summary>
/// 创建审计对象
/// </summary>
public interface ICreationAuditedObject : IHasCreationTime
{
    /// <summary>
    /// 创建者 ID
    /// </summary>
    string? CreatorId { get; }
}

/// <summary>
/// 有修改时间的对象
/// </summary>
public interface IHasModificationTime
{
    /// <summary>
    /// 最后修改时间
    /// </summary>
    DateTime? LastModificationTime { get; }
}

/// <summary>
/// 修改审计对象
/// </summary>
public interface IModificationAuditedObject : IHasModificationTime
{
    /// <summary>
    /// 最后修改者 ID
    /// </summary>
    string? LastModifierId { get; }
}

/// <summary>
/// 有删除时间的对象
/// </summary>
public interface IHasDeletionTime
{
    /// <summary>
    /// 删除时间
    /// </summary>
    DateTime? DeletionTime { get; }
}

/// <summary>
/// 软删除对象
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// 是否已删除
    /// </summary>
    bool IsDeleted { get; }
}

/// <summary>
/// 删除审计对象
/// </summary>
public interface IDeletionAuditedObject : IHasDeletionTime, ISoftDelete
{
    /// <summary>
    /// 删除者 ID
    /// </summary>
    string? DeleterId { get; }
}

/// <summary>
/// 完整审计对象
/// </summary>
public interface IFullAuditedObject :
    ICreationAuditedObject,
    IModificationAuditedObject,
    IDeletionAuditedObject
{
}

namespace Leistd.Ddd.Domain.Auditing;

/// <summary>
/// 审计属性设置器接口
/// </summary>
/// <remarks>
/// 负责自动设置实体的审计属性（创建时间、修改时间、删除时间等）
/// 注意：参数使用 object 而非 EntityEntry 以避免 Domain 层依赖 EF Core Infrastructure
/// </remarks>
public interface IAuditPropertySetter
{
    /// <summary>
    /// 设置创建相关的审计属性
    /// </summary>
    /// <param name="entityEntry">实体或实体变更追踪条目（Infrastructure 层会传入 EntityEntry）</param>
    void SetCreationProperties(object entityEntry);

    /// <summary>
    /// 设置修改相关的审计属性
    /// </summary>
    /// <param name="entityEntry">实体或实体变更追踪条目（Infrastructure 层会传入 EntityEntry）</param>
    void SetModificationProperties(object entityEntry);

    /// <summary>
    /// 设置删除相关的审计属性（软删除）
    /// </summary>
    /// <param name="entityEntry">实体或实体变更追踪条目（Infrastructure 层会传入 EntityEntry）</param>
    void SetDeletionProperties(object entityEntry);
}

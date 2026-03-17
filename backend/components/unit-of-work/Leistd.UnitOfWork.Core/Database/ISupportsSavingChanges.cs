namespace Leistd.UnitOfWork.Core.Database;

/// <summary>
/// 支持保存更改的接口
/// </summary>
public interface ISupportsSavingChanges
{
    /// <summary>
    /// 保存更改
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

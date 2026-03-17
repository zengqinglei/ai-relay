namespace Leistd.UnitOfWork.Core.Database;

/// <summary>
/// 支持回滚操作的接口
/// </summary>
public interface ISupportsRollback
{
    /// <summary>
    /// 回滚
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

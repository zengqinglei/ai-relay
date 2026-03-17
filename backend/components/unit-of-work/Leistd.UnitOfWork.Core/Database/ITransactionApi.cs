namespace Leistd.UnitOfWork.Core.Database;

/// <summary>
/// 事务 API 接口
/// </summary>
public interface ITransactionApi : IDisposable
{
    /// <summary>
    /// 提交事务
    /// </summary>
    Task CommitAsync();
}

namespace Leistd.UnitOfWork.Core.Database;

/// <summary>
/// 事务 API 容器接口
/// </summary>
public interface ITransactionApiContainer
{
    /// <summary>
    /// 查找事务 API
    /// </summary>
    ITransactionApi? FindTransactionApi();

    /// <summary>
    /// 添加事务 API
    /// </summary>
    void AddTransactionApi(ITransactionApi api);
}

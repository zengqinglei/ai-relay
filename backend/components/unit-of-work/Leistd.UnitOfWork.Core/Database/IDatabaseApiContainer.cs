namespace Leistd.UnitOfWork.Core.Database;

/// <summary>
/// 数据库 API 容器接口
/// </summary>
public interface IDatabaseApiContainer
{
    /// <summary>
    /// 获取或添加数据库 API
    /// </summary>
    IDatabaseApi GetOrAddDatabaseApi(Func<IDatabaseApi> factory);
}

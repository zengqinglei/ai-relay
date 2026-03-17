using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Leistd.UnitOfWork.EfCore.Database;

/// <summary>
/// DbContext 扩展方法
/// </summary>
internal static class DbContextExtensions
{
    /// <summary>
    /// 检查 DbContext 是否使用关系型数据库事务管理器
    /// </summary>
    /// <param name="dbContext">DbContext 实例</param>
    /// <returns>如果使用关系型事务管理器返回 true，否则返回 false</returns>
    public static bool HasRelationalTransactionManager(this DbContext dbContext)
    {
        return dbContext.Database.GetService<IDbContextTransactionManager>() is IRelationalTransactionManager;
    }
}

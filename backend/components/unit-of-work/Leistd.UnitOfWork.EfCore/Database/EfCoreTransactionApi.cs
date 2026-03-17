using Leistd.UnitOfWork.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Leistd.UnitOfWork.EfCore.Database;

/// <summary>
/// EF Core Transaction API implementation
/// </summary>
public class EfCoreTransactionApi(IDbContextTransaction dbContextTransaction, DbContext starterDbContext)
    : ITransactionApi, ISupportsRollback
{
    public IDbContextTransaction DbContextTransaction { get; } = dbContextTransaction;

    /// <summary>
    /// 启动事务的 DbContext
    /// </summary>
    public DbContext StarterDbContext { get; } = starterDbContext;

    /// <summary>
    /// 附加到此事务的其他 DbContext
    /// </summary>
    public List<DbContext> AttendedDbContexts { get; } = new();

    public async Task CommitAsync()
    {
        // 先处理所有 AttendedDbContexts
        foreach (var dbContext in AttendedDbContexts)
        {
            // 关系型数据库且共享同一连接时，跳过（会随主事务一起提交）
            if (dbContext.HasRelationalTransactionManager() &&
                dbContext.Database.GetDbConnection() == DbContextTransaction.GetDbTransaction().Connection)
            {
                continue;
            }

            // 非关系型数据库或使用不同连接的数据库，需要单独提交
            await dbContext.Database.CommitTransactionAsync();
        }

        // 最后提交主事务
        await DbContextTransaction.CommitAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        // 先处理所有 AttendedDbContexts
        foreach (var dbContext in AttendedDbContexts)
        {
            // 关系型数据库且共享同一连接时，跳过（会随主事务一起回滚）
            if (dbContext.HasRelationalTransactionManager() &&
                dbContext.Database.GetDbConnection() == DbContextTransaction.GetDbTransaction().Connection)
            {
                continue;
            }

            // 非关系型数据库或使用不同连接的数据库，需要单独回滚
            await dbContext.Database.RollbackTransactionAsync(cancellationToken);
        }

        // 最后回滚主事务
        await DbContextTransaction.RollbackAsync(cancellationToken);
    }

    public void Dispose()
    {
        DbContextTransaction.Dispose();
    }
}

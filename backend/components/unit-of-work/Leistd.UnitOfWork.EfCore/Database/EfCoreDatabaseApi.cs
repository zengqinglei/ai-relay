using Leistd.UnitOfWork.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace Leistd.UnitOfWork.EfCore.Database;

/// <summary>
/// EF Core Database API implementation
/// </summary>
public class EfCoreDatabaseApi<TDbContext>(TDbContext dbContext) : IDatabaseApi, ISupportsSavingChanges, ISupportsRollback
    where TDbContext : DbContext
{
    public TDbContext DbContext { get; } = dbContext;

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        // EF Core doesn't directly support rollback on DbContext level
        // Rollback is handled by transaction
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // DbContext 由 DI 容器管理（Scoped 生命周期）
        // 会在 UnitOfWork 的 Scope 释放时自动 Dispose
        // 因此这里不需要显式释放，避免重复 Dispose 导致的问题
        //
        // 说明：
        // 1. DbContext 通过 IDbContextProvider 从 DI 容器获取（Scoped）
        // 2. UnitOfWorkManager.CreateNewUnitOfWork() 创建独立的 Scope
        // 3. UnitOfWork.Disposed 事件触发时会释放 Scope
        // 4. Scope 释放时会自动 Dispose 所有 Scoped 服务（包括 DbContext）
    }
}

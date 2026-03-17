using Leistd.UnitOfWork.Core.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.UnitOfWork.EfCore.Database;

/// <summary>
/// DbContext provider implementation
/// 支持两种模式：
/// 1. 默认模式：不需要 UnitOfWork，直接从 Scoped 容器获取 DbContext（适合简单 CRUD）
/// 2. UnitOfWork 模式：在 UnitOfWork 内，从 UnitOfWork 的 Scope 获取 DbContext（适合事务场景）
/// </summary>
public class DbContextProvider<TDbContext>(
    IUnitOfWorkManager unitOfWorkManager,
    IServiceProvider serviceProvider) : IDbContextProvider<TDbContext>
    where TDbContext : DbContext
{
    public async Task<TDbContext> GetDbContextAsync(CancellationToken cancellationToken = default)
    {
        var unitOfWork = unitOfWorkManager.Current;

        // 模式 1：没有 UnitOfWork - 直接从当前 Scoped 容器获取
        if (unitOfWork == null)
        {
            // 不在 UnitOfWork 内，直接从 Scoped ServiceProvider 获取 DbContext
            // 适用于简单的 CRUD 操作，无需显式开启事务
            return serviceProvider.GetRequiredService<TDbContext>();
        }

        // 模式 2：有 UnitOfWork - 从 UnitOfWork 的 Scope 获取
        // 从 UnitOfWork 获取 ServiceProvider（参考 ABP 设计）
        var uowServiceProvider = GetServiceProvider(unitOfWork);

        // 在 UnitOfWork 内，从 UnitOfWork 获取或创建 DbContext
        var databaseApi = unitOfWork.GetOrAddDatabaseApi(() =>
        {
            var dbContext = unitOfWork.Options.IsTransactional
                ? CreateDbContextWithTransactionAsync(unitOfWork, uowServiceProvider, cancellationToken).Result  // 需要事务
                : uowServiceProvider.GetRequiredService<TDbContext>();      // 不需要事务

            // 应用 Timeout 设置
            ApplyTimeout(dbContext, unitOfWork);

            return new EfCoreDatabaseApi<TDbContext>(dbContext);
        });

        var efCoreApi = (EfCoreDatabaseApi<TDbContext>)databaseApi;
        return efCoreApi.DbContext;
    }

    private async Task<TDbContext> CreateDbContextWithTransactionAsync(
        IUnitOfWork unitOfWork,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var activeTransaction = unitOfWork.FindTransactionApi() as EfCoreTransactionApi;

        if (activeTransaction == null)
        {
            // 场景 1：第一个 DbContext，创建新事务
            var dbContext = serviceProvider.GetRequiredService<TDbContext>();

            // 创建数据库事务
            var dbTransaction = unitOfWork.Options.IsolationLevel.HasValue
                ? await dbContext.Database.BeginTransactionAsync(unitOfWork.Options.IsolationLevel.Value, cancellationToken)
                : await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // 创建 TransactionApi 并添加到 UnitOfWork
            var transactionApi = new EfCoreTransactionApi(dbTransaction, dbContext);
            unitOfWork.AddTransactionApi(transactionApi);

            return dbContext;
        }
        else
        {
            // 场景 2：已有事务，复用或添加到 AttendedDbContexts
            var dbContext = serviceProvider.GetRequiredService<TDbContext>();

            if (dbContext.HasRelationalTransactionManager())
            {
                // 关系型数据库：使用 UseTransaction 共享事务
                await dbContext.Database.UseTransactionAsync(activeTransaction.DbContextTransaction.GetDbTransaction(), cancellationToken);
            }
            else
            {
                // 非关系型数据库：创建新事务
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            }

            // 添加到 AttendedDbContexts，以便在 Commit/Rollback 时统一处理
            activeTransaction.AttendedDbContexts.Add(dbContext);

            return dbContext;
        }
    }

    private void ApplyTimeout(TDbContext dbContext, IUnitOfWork unitOfWork)
    {
        if (unitOfWork.Options.Timeout.HasValue)
        {
            // 检查是否为关系型数据库
            if (dbContext.Database.IsRelational())
            {
                // 只在未设置 CommandTimeout 时应用
                if (!dbContext.Database.GetCommandTimeout().HasValue)
                {
                    dbContext.Database.SetCommandTimeout((int)unitOfWork.Options.Timeout.Value.TotalSeconds);
                }
            }
        }
    }

    /// <summary>
    /// 从 UnitOfWork 获取 ServiceProvider（参考 ABP 设计）
    /// 由于 IUnitOfWork 接口不暴露 ServiceProvider，需要转换为具体类型
    /// </summary>
    private static IServiceProvider GetServiceProvider(IUnitOfWork unitOfWork)
    {
        // 转换为具体的 UnitOfWork 类型以访问 ServiceProvider
        if (unitOfWork is Core.Uow.UnitOfWork concreteUow)
        {
            return concreteUow.ServiceProvider;
        }

        throw new InvalidOperationException(
            $"Cannot get ServiceProvider from UnitOfWork. " +
            $"Expected type: {typeof(Core.Uow.UnitOfWork).FullName}, " +
            $"Actual type: {unitOfWork.GetType().FullName}");
    }
}

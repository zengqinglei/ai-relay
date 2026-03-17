using Leistd.Ddd.Domain.Entities;
using Leistd.Ddd.Domain.Repositories;
using Leistd.UnitOfWork.Core.Uow;
using Leistd.UnitOfWork.EfCore.Database;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Leistd.Ddd.Infrastructure.Persistence.Repositories;

public class EfCoreRepository<TDbContext, TEntity>(
    IDbContextProvider<TDbContext> dbContextProvider,
    IUnitOfWorkManager uow) : BaseRepository<TEntity>
    where TDbContext : DbContext
    where TEntity : class, IEntity
{
    protected readonly IDbContextProvider<TDbContext> DbContextProvider = dbContextProvider;
    protected readonly IUnitOfWorkManager Uow = uow;

    protected async Task<TDbContext> GetDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await DbContextProvider.GetDbContextAsync(cancellationToken);
    }

    protected virtual async Task<DbSet<TEntity>> GetDbSetAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync(cancellationToken);
        return dbContext.Set<TEntity>();
    }

    public override async Task<IQueryable<TEntity>> GetQueryableAsync(CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return dbSet.AsQueryable();
    }

    public override async Task<TEntity?> GetFirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return await dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public override async Task<IEnumerable<TEntity>> GetListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return predicate == null
            ? await dbSet.ToListAsync(cancellationToken)
            : await dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public override async Task<TEntity?> GetOneAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return await dbSet.SingleOrDefaultAsync(predicate, cancellationToken);
    }

    public override async Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return predicate == null
            ? await dbSet.LongCountAsync(cancellationToken)
            : await dbSet.LongCountAsync(predicate, cancellationToken);
    }

    public override async Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return predicate == null
            ? await dbSet.AnyAsync(cancellationToken)
            : await dbSet.AnyAsync(predicate, cancellationToken);
    }

    public override async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        var entry = await dbSet.AddAsync(entity, cancellationToken);
        await SaveChangesIfNeededAsync(cancellationToken);
        return entry.Entity;
    }

    public override async Task InsertManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        await dbSet.AddRangeAsync(entities, cancellationToken);
        await SaveChangesIfNeededAsync(cancellationToken);
    }

    public override async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        var entry = dbSet.Update(entity);
        await SaveChangesIfNeededAsync(cancellationToken);
        return entry.Entity;
    }

    public override async Task UpdateManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        dbSet.UpdateRange(entities);
        await SaveChangesIfNeededAsync(cancellationToken);
    }

    public override async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        dbSet.Remove(entity);
        await SaveChangesIfNeededAsync(cancellationToken);
    }

    public override async Task DeleteManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        dbSet.RemoveRange(entities);
        await SaveChangesIfNeededAsync(cancellationToken);
    }

    public override async Task DeleteManyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        var entities = await dbSet.Where(predicate).ToListAsync(cancellationToken);
        dbSet.RemoveRange(entities);
        await SaveChangesIfNeededAsync(cancellationToken);
    }

    /// <summary>
    /// 智能保存：如果在 UnitOfWork 内则不保存（由 UOW 统一管理），否则立即保存
    /// </summary>
    protected async Task SaveChangesIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (Uow.Current != null)
        {
            return;
        }

        var dbContext = await GetDbContextAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}


public class EfCoreRepository<TDbContext, TEntity, TKey>(
    IDbContextProvider<TDbContext> dbContextProvider,
    IUnitOfWorkManager uow) : EfCoreRepository<TDbContext, TEntity>(dbContextProvider, uow), IRepository<TEntity, TKey>
    where TDbContext : DbContext
    where TKey : IEquatable<TKey>
    where TEntity : class, IEntity<TKey>
{

    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return await dbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }

    public virtual async Task DeleteManyAsync([NotNull] IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        var entities = await dbSet.Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);
        dbSet.RemoveRange(entities);
        await SaveChangesIfNeededAsync(cancellationToken);
    }
}
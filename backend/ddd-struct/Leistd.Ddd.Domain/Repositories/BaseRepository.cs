using Leistd.Ddd.Domain.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Leistd.Ddd.Domain.Repositories;

public abstract class BaseRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IEntity
{
    public abstract Task<IQueryable<TEntity>> GetQueryableAsync(CancellationToken cancellationToken = default);
    public abstract Task<TEntity?> GetFirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    public abstract Task<TEntity?> GetOneAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    public abstract Task<IEnumerable<TEntity>> GetListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    public abstract Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    public abstract Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    public abstract Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);
    public abstract Task InsertManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    public abstract Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    public abstract Task UpdateManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    public abstract Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
    public abstract Task DeleteManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    public abstract Task DeleteManyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}

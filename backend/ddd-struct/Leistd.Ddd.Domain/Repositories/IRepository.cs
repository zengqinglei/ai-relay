using Leistd.Ddd.Domain.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Leistd.Ddd.Domain.Repositories;

/// <summary>
/// This interface must be implemented by all repositories to identify them by convention.
/// Implement generic version instead of this one.
/// </summary>
public interface IRepository
{
}

public interface IRepository<TEntity> : IRepository where TEntity : class, IEntity
{
    Task<IQueryable<TEntity>> GetQueryableAsync(CancellationToken cancellationToken = default);

    Task<TEntity?> GetOneAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<TEntity?> GetFirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<IEnumerable<TEntity>> GetListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task InsertManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task UpdateManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task DeleteManyAsync([NotNull] IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    Task DeleteManyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}

public interface IRepository<TEntity, TKey> : IRepository<TEntity> where TEntity : class, IEntity<TKey>
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);

    Task DeleteManyAsync([NotNull] IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
}
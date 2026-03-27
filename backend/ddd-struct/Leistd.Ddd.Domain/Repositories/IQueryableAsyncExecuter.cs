namespace Leistd.Ddd.Domain.Repositories;

/// <summary>
/// IQueryable 异步执行器接口
/// </summary>
public interface IQueryableAsyncExecuter
{
    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
}

using Leistd.Ddd.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leistd.Ddd.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core 实现的 IQueryable 异步执行器
/// </summary>
public class EfCoreQueryableAsyncExecuter : IQueryableAsyncExecuter
{
    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken);
    }

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);
    }

    public Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return EntityFrameworkQueryableExtensions.LongCountAsync(query, cancellationToken);
    }

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);
    }

    public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(query, cancellationToken);
    }

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return EntityFrameworkQueryableExtensions.AnyAsync(query, cancellationToken);
    }
}

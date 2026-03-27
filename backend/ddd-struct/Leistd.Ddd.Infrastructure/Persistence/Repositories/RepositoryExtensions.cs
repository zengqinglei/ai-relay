using Leistd.Ddd.Domain.Entities;
using Leistd.Ddd.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Leistd.Ddd.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository 扩展方法（参考 Creekdream）
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    /// 异步获取带导航属性的查询（Include）
    /// </summary>
    public static async Task<IQueryable<TEntity>> GetQueryIncludingAsync<TEntity, TKey>(
        this IRepository<TEntity, TKey> repository,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] propertySelectors)
        where TEntity : class, IEntity<TKey>
    {
        var query = await repository.GetQueryableAsync(cancellationToken);

        if (propertySelectors != null && propertySelectors.Any())
        {
            foreach (var propertySelector in propertySelectors)
            {
                query = query.Include(propertySelector);
            }
        }

        return query;
    }
}


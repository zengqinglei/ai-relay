using System.Linq.Dynamic.Core;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ApiKeys.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.UnitOfWork.Core.Uow;
using Leistd.UnitOfWork.EfCore.Database;
using Microsoft.EntityFrameworkCore;

namespace AiRelay.Infrastructure.Persistence.Repositories;

public class ApiKeyRepository(
    IDbContextProvider<AiRelayDbContext> dbContextProvider,
    IUnitOfWorkManager uow)
    : EfCoreRepository<AiRelayDbContext, ApiKey, Guid>(dbContextProvider, uow), IApiKeyRepository
{
    public async Task<ApiKey?> GetWithBindingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return await dbSet
            .Include(x => x.Bindings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<ApiKey?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return await dbSet
            .Include(x => x.Bindings)
                .ThenInclude(b => b.ProviderGroup)
                    .ThenInclude(g => g.Relations)
                        .ThenInclude(r => r.AccountToken)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(long TotalCount, List<ApiKey> Items)> GetPagedListAsync(
        string? keyword,
        bool? isActive,
        int offset,
        int limit,
        string? sorting = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        var query = dbSet
            .Include(x => x.Bindings)
                .ThenInclude(b => b.ProviderGroup)
                    .ThenInclude(g => g.Relations)
                        .ThenInclude(r => r.AccountToken)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(k => k.Name.Contains(keyword));
        }

        if (isActive.HasValue)
        {
            query = query.Where(k => k.IsActive == isActive.Value);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var sortExpression = string.IsNullOrWhiteSpace(sorting)
            ? $"{nameof(ApiKey.CreationTime)} desc"
            : sorting;

        var items = await query
            .OrderBy(sortExpression)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (totalCount, items);
    }
}

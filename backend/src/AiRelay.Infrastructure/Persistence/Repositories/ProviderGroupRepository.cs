using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.UnitOfWork.Core.Uow;
using Leistd.UnitOfWork.EfCore.Database;
using Microsoft.EntityFrameworkCore;

namespace AiRelay.Infrastructure.Persistence.Repositories;

public class ProviderGroupRepository(
    IDbContextProvider<AiRelayDbContext> dbContextProvider,
    IUnitOfWorkManager uow)
    : EfCoreRepository<AiRelayDbContext, ProviderGroup, Guid>(dbContextProvider, uow), IProviderGroupRepository
{
    public async Task<ProviderGroup?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return await dbSet
            .Include(x => x.Relations)
            .Include(x => x.ApiKeyBindings)
            .Include(x => x.AssignedUsers)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<ProviderGroup>> GetVisibleGroupsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return await dbSet
            .Include(x => x.AssignedUsers)
            .Where(x => !x.AssignedUsers.Any() || x.AssignedUsers.Any(y => y.UserId == userId))
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProviderGroup?> GetVisibleByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);
        return await dbSet
            .Include(x => x.AssignedUsers)
            .SingleOrDefaultAsync(
                x => x.Id == id && (!x.AssignedUsers.Any() || x.AssignedUsers.Any(y => y.UserId == userId)),
                cancellationToken);
    }
}

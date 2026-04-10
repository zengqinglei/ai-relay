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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}

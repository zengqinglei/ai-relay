using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.UnitOfWork.Core.Uow;
using Leistd.UnitOfWork.EfCore.Database;
using Microsoft.EntityFrameworkCore;

namespace AiRelay.Infrastructure.Persistence.Repositories;

public class ProviderGroupAccountRelationRepository(
    IDbContextProvider<AiRelayDbContext> dbContextProvider,
    IUnitOfWorkManager uow)
    : EfCoreRepository<AiRelayDbContext, ProviderGroupAccountRelation, Guid>(dbContextProvider, uow), IProviderGroupAccountRelationRepository
{
    public async Task<List<ProviderGroupAccountRelation>> GetListByGroupIdWithAccountsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);

        // 使用 Include 预加载 AccountToken
        // 过滤条件：分组ID匹配 && 关联关系启用 && 账户启用
        return await dbSet
            .Include(r => r.AccountToken)
            .Where(r => r.ProviderGroupId == groupId && r.IsActive && r.AccountToken != null && r.AccountToken.IsActive)
            .ToListAsync(cancellationToken);
    }
}

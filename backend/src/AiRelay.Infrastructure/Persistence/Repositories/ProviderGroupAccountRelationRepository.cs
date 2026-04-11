using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.UnitOfWork.Core.Uow;
using Leistd.UnitOfWork.EfCore.Database;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AiRelay.Infrastructure.Persistence.Repositories;

public class ProviderGroupAccountRelationRepository(
    IDbContextProvider<AiRelayDbContext> dbContextProvider,
    IUnitOfWorkManager uow)
    : EfCoreRepository<AiRelayDbContext, ProviderGroupAccountRelation, Guid>(dbContextProvider, uow), IProviderGroupAccountRelationRepository
{
    public async Task<List<ProviderGroupAccountRelation>> GetCandidatesAsync(
        Guid groupId,
        List<(Provider Provider, AuthMethod AuthMethod)>? allowedCombinations = null,
        List<Guid>? excludedAccountIds = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync(cancellationToken);

        // 使用 Include 预加载 AccountToken
        // 基础过滤条件：分组ID匹配 && 关联关系启用 && 账户存在且启用
        var query = dbSet
            .Include(r => r.AccountToken)
            .Where(r => r.ProviderGroupId == groupId && r.IsActive && r.AccountToken != null && r.AccountToken.IsActive);

        // SQL 过滤：排除指定的账号 ID
        if (excludedAccountIds != null && excludedAccountIds.Count > 0)
        {
            query = query.Where(r => !excludedAccountIds.Contains(r.AccountTokenId));
        }

        // SQL 过滤：根据 (Provider, AuthMethod) 组合过滤（协议降级/隔离）
        if (allowedCombinations != null && allowedCombinations.Count > 0)
        {
            // EF Core 无法直接翻译内存集合的 Any(Tuple) 匹配。
            // 我们通过构造表达式树生成：(p == p1 && a == a1) OR (p == p2 && a == a2) ...
            var parameter = Expression.Parameter(typeof(ProviderGroupAccountRelation), "r");
            Expression? compoundExpression = null;

            foreach (var combo in allowedCombinations)
            {
                // r.AccountToken.Provider == combo.Provider
                var providerProperty = Expression.Property(Expression.Property(parameter, nameof(ProviderGroupAccountRelation.AccountToken)), nameof(AccountToken.Provider));
                var providerCondition = Expression.Equal(providerProperty, Expression.Constant(combo.Provider));

                // r.AccountToken.AuthMethod == combo.AuthMethod
                var authMethodProperty = Expression.Property(Expression.Property(parameter, nameof(ProviderGroupAccountRelation.AccountToken)), nameof(AccountToken.AuthMethod));
                var authMethodCondition = Expression.Equal(authMethodProperty, Expression.Constant(combo.AuthMethod));

                // (Provider == Provider && AuthMethod == AuthMethod)
                var combinedCondition = Expression.AndAlso(providerCondition, authMethodCondition);

                compoundExpression = compoundExpression == null 
                    ? combinedCondition 
                    : Expression.OrElse(compoundExpression, combinedCondition);
            }

            if (compoundExpression != null)
            {
                var lambda = Expression.Lambda<Func<ProviderGroupAccountRelation, bool>>(compoundExpression, parameter);
                query = query.Where(lambda);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }
}

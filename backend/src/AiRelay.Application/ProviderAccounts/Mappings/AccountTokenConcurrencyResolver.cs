using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AutoMapper;

namespace AiRelay.Application.ProviderAccounts.Mappings;

public class AccountTokenConcurrencyResolver(IConcurrencyStrategy concurrencyStrategy) :
    IValueResolver<AccountToken, AccountTokenOutputDto, int>,
    IValueResolver<AccountToken, AvailableAccountTokenOutputDto, int>
{
    public int Resolve(AccountToken source, AccountTokenOutputDto destination, int destMember, ResolutionContext context)
    {
        if (context.Items.TryGetValue("ConcurrencyCounts", out var countsObj) &&
            countsObj is IDictionary<Guid, int> counts &&
            counts.TryGetValue(source.Id, out var count))
        {
            return count;
        }

        return concurrencyStrategy.GetConcurrencyCountAsync(source.Id).GetAwaiter().GetResult();
    }

    public int Resolve(AccountToken source, AvailableAccountTokenOutputDto destination, int destMember, ResolutionContext context)
    {
        if (context.Items.TryGetValue("ConcurrencyCounts", out var countsObj) &&
            countsObj is IDictionary<Guid, int> counts &&
            counts.TryGetValue(source.Id, out var count))
        {
            return count;
        }

        return concurrencyStrategy.GetConcurrencyCountAsync(source.Id).GetAwaiter().GetResult();
    }
}

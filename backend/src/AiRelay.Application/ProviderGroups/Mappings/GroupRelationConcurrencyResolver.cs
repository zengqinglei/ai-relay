using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ProviderGroups.Entities;
using AutoMapper;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;

namespace AiRelay.Application.ProviderGroups.Mappings;

public class GroupRelationConcurrencyResolver(IConcurrencyStrategy concurrencyStrategy) : IValueResolver<ProviderGroupAccountRelation, GroupAccountRelationOutputDto, int>
{
    public int Resolve(ProviderGroupAccountRelation source, GroupAccountRelationOutputDto destination, int destMember, ResolutionContext context)
    {
        // 1. 优先从 Context 获取批量预取的数据
        if (context.Items.TryGetValue("ConcurrencyCounts", out var countsObj) &&
            countsObj is IDictionary<Guid, int> counts &&
            counts.TryGetValue(source.AccountTokenId, out var count))
        {
            return count;
        }

        // 2. 兜底：自行查询 (Sync-over-Async)
        return concurrencyStrategy.GetConcurrencyCountAsync(source.AccountTokenId).GetAwaiter().GetResult();
    }
}

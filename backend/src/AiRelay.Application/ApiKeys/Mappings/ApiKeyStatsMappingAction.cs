using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.ApiKeys.Entities;
using AutoMapper;

namespace AiRelay.Application.ApiKeys.Mappings;

public class ApiKeyStatsMappingAction : IMappingAction<ApiKey, ApiKeyOutputDto>
{
    public void Process(ApiKey source, ApiKeyOutputDto destination, ResolutionContext context)
    {
        destination.UsageToday = source.UsageToday;
        destination.UsageTotal = source.UsageTotal;
        destination.CostToday = source.CostToday;
        destination.CostTotal = source.CostTotal;
        destination.TokensToday = source.TokensToday;
        destination.TokensTotal = source.TokensTotal;
    }
}

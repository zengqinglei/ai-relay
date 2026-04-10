using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.ObjectMapping.Mapster;

namespace AiRelay.Application.ApiKeys.Mappings;

public class ApiKeyProfile : MapsterProfile
{
    protected override void ConfigureMappings()
    {
        CreateMap<ApiKey, ApiKeyOutputDto>()
            .Map(dest => dest.Secret, src => "***") // 具体解密已在 ApiKeyAppService 中显式进行
            .AfterMapping((src, dest) =>
            {
                dest.UsageToday = src.UsageToday;
                dest.UsageTotal = src.UsageTotal;
                dest.CostToday = src.CostToday;
                dest.CostTotal = src.CostTotal;
                dest.TokensToday = src.TokensToday;
                dest.TokensTotal = src.TokensTotal;
            });

        CreateMap<ApiKeyProviderGroupBinding, ApiKeyBindingOutputDto>()
            .Map(dest => dest.ProviderGroupName, src => src.ProviderGroup.Name)
            .Map(dest => dest.SupportedRouteProfiles, src => ResolveRouteProfiles(src));
    }

    private static List<RouteProfile> ResolveRouteProfiles(ApiKeyProviderGroupBinding binding)
    {
        var relations = binding.ProviderGroup?.Relations;
        if (relations == null || !relations.Any())
            return [];

        var combinations = relations
            .Where(r => r.AccountToken != null)
            .Select(r => (r.AccountToken!.Provider, r.AccountToken!.AuthMethod))
            .ToHashSet();

        return RouteProfileRegistry.Profiles
            .Where(p => p.Value.SupportedCombinations.Any(c => combinations.Contains((c.Provider, c.AuthMethod))))
            .Select(p => p.Key)
            .OrderBy(p => p)
            .ToList();
    }
}

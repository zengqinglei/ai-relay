using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.ApiKeys.Entities;
using Leistd.ObjectMapping.Mapster;
using Mapster;

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
            .Map(dest => dest.ProviderGroupName, src => src.ProviderGroup.Name);
    }
}

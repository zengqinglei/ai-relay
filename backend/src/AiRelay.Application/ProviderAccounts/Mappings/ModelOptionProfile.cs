using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using Leistd.ObjectMapping.Mapster;

namespace AiRelay.Application.ProviderAccounts.Mappings;

/// <summary>
/// Model Option Mapster 配置
/// </summary>
public class ModelOptionProfile : MapsterProfile
{
    protected override void ConfigureMappings()
    {
        CreateMap<ModelOption, ModelOptionOutputDto>();
    }
}

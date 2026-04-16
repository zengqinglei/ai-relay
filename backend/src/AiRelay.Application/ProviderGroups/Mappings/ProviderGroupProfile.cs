using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderGroups.Entities;
using Leistd.ObjectMapping.Mapster;

namespace AiRelay.Application.ProviderGroups.Mappings;

/// <summary>
/// 提供商分组 Mappings 配置
/// </summary>
public class ProviderGroupProfile : MapsterProfile
{
    protected override void ConfigureMappings()
    {
        CreateMap<ProviderGroup, ProviderGroupOutputDto>();

        CreateMap<ApiKeyProviderGroupBinding, ApiKeyBindingOutputDto>()
            .Map(d => d.ProviderGroupName, s => s.ProviderGroup.Name);
    }
}

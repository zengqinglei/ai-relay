using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Application.ApiKeys.Dtos;
using AutoMapper;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ApiKeys.Entities;

namespace AiRelay.Application.ProviderGroups.Mappings;

/// <summary>
/// 提供商分组 AutoMapper 配置
/// </summary>
public class ProviderGroupProfile : Profile
{
    public ProviderGroupProfile()
    {
        CreateMap<ProviderGroup, ProviderGroupOutputDto>()
            .ForMember(d => d.Accounts, o => o.Ignore()); // Accounts 需要手动加载或在查询时 Include

        CreateMap<ProviderGroupAccountRelation, GroupAccountRelationOutputDto>()
            .ForMember(d => d.AccountTokenName, o => o.MapFrom(s => s.AccountToken!.Name))
            .ForMember(d => d.IsActive, o => o.MapFrom(s => s.AccountToken!.IsActive))
            .ForMember(d => d.ExpiresAt, o => o.MapFrom(s => s.AccountToken!.ExpiresAt))
            .ForMember(d => d.MaxConcurrency, o => o.MapFrom(s => s.AccountToken!.MaxConcurrency))
            .ForMember(d => d.CurrentConcurrency, o => o.MapFrom<GroupRelationConcurrencyResolver>());

        CreateMap<ApiKeyProviderGroupBinding, ApiKeyBindingOutputDto>()
            .ForMember(d => d.ProviderGroupName, o => o.MapFrom(s => s.ProviderGroup.Name));
    }
}

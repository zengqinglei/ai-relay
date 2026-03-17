using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.Extensions;
using AutoMapper;

namespace AiRelay.Application.ProviderAccounts.Mappings;

public class AccountTokenProfile : Profile
{
    public AccountTokenProfile()
    {
        CreateMap<AccountToken, AvailableAccountTokenOutputDto>()
            .ForMember(dest => dest.CurrentConcurrency, opt => opt.MapFrom<AccountTokenConcurrencyResolver>());

        CreateMap<AccountToken, AccountTokenOutputDto>()
            .ForMember(dest => dest.FullToken, opt => opt.MapFrom(src =>
                src.Platform.IsApiKeyPlatform() ? src.AccessToken ?? string.Empty : src.RefreshToken ?? string.Empty))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.GetEffectiveStatus()))
            .ForMember(dest => dest.CurrentConcurrency, opt => opt.MapFrom<AccountTokenConcurrencyResolver>())
            .AfterMap<AccountTokenStatsMappingAction>();
    }
}

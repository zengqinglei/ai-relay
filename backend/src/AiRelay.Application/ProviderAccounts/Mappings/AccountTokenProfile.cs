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
                MaskToken(src.Platform.IsApiKeyPlatform() ? src.AccessToken : src.RefreshToken)))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.GetEffectiveStatus()))
            .ForMember(dest => dest.CurrentConcurrency, opt => opt.MapFrom<AccountTokenConcurrencyResolver>())
            .AfterMap<AccountTokenStatsMappingAction>();
    }

    private static string MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 12)
            return "***";

        return $"{token.Substring(0, 7)}...{token.Substring(token.Length - 4)}";
    }
}

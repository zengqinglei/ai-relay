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
            .ForMember(dest => dest.SuccessRateToday, opt => opt.MapFrom(src =>
                src.UsageToday > 0 ? Math.Round(src.SuccessToday * 100m / src.UsageToday, 1) : 0m))
            .ForMember(dest => dest.SuccessRateTotal, opt => opt.MapFrom(src =>
                src.UsageTotal > 0 ? Math.Round(src.SuccessTotal * 100m / src.UsageTotal, 1) : 0m));
    }

    private static string MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 12)
            return "***";

        return $"{token.Substring(0, 7)}...{token.Substring(token.Length - 4)}";
    }
}

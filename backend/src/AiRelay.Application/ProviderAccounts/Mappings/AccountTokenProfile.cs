using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.ObjectMapping.Mapster;
using Mapster;

namespace AiRelay.Application.ProviderAccounts.Mappings;

public class AccountTokenProfile : MapsterProfile
{
    protected override void ConfigureMappings()
    {
        CreateMap<LimitedModelState, LimitedModelStateDto>();

        CreateMap<AccountToken, AvailableAccountTokenOutputDto>()
            .Map(dest => dest.CurrentConcurrency, src => ResolveConcurrencyCount(src));

        CreateMap<AccountToken, AccountTokenOutputDto>()
            .Map(dest => dest.FullToken, src =>
                MaskToken(src.AuthMethod == AiRelay.Domain.ProviderAccounts.ValueObjects.AuthMethod.ApiKey ? src.AccessToken : src.RefreshToken))
            .Map(dest => dest.Status, src => src.GetEffectiveStatus())
            .Map(dest => dest.CurrentConcurrency, src => ResolveConcurrencyCount(src))
            .Map(dest => dest.LimitedModels, src => src.GetActiveLimitedModels())
            .Map(dest => dest.LimitedModelCount, src => src.GetActiveLimitedModels().Count)
            .Map(dest => dest.SuccessRateToday, src =>
                src.UsageToday > 0 ? Math.Round(src.SuccessToday * 100m / src.UsageToday, 1) : 0m)
            .Map(dest => dest.SuccessRateTotal, src =>
                src.UsageTotal > 0 ? Math.Round(src.SuccessTotal * 100m / src.UsageTotal, 1) : 0m);
    }

    private static int ResolveConcurrencyCount(AccountToken source)
    {
        if (MapContext.Current?.Parameters.TryGetValue("ConcurrencyCounts", out var countsObj) == true &&
            countsObj is IDictionary<Guid, int> counts &&
            counts.TryGetValue(source.Id, out var count))
        {
            return count;
        }

        // 预获取已在 AppService 中完成，如果缺失则兜底返回 0
        return 0;
    }

    private static string MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 12)
            return "***";

        return $"{token.Substring(0, 7)}...{token.Substring(token.Length - 4)}";
    }
}

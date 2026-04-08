using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ApiKeys.Entities;
using Leistd.ObjectMapping.Mapster;
using Mapster;

namespace AiRelay.Application.ProviderGroups.Mappings;

/// <summary>
/// 提供商分组 Mappings 配置
/// </summary>
public class ProviderGroupProfile : MapsterProfile
{
    protected override void ConfigureMappings()
    {
        CreateMap<ProviderGroup, ProviderGroupOutputDto>()
            .Ignore(d => d.Accounts); // Accounts 需要手动加载或在查询时 Include

        CreateMap<ProviderGroupAccountRelation, GroupAccountRelationOutputDto>()
            .Map(d => d.AccountTokenName, s => s.AccountToken!.Name)
            .Map(d => d.IsActive, s => s.AccountToken!.IsActive)
            .Map(d => d.ExpiresAt, s => s.AccountToken!.ExpiresAt)
            .Map(d => d.MaxConcurrency, s => s.AccountToken!.MaxConcurrency)
            .Map(d => d.CurrentConcurrency, s => ResolveConcurrencyCount(s));

        CreateMap<ApiKeyProviderGroupBinding, ApiKeyBindingOutputDto>()
            .Map(d => d.ProviderGroupName, s => s.ProviderGroup.Name);
    }

    private static int ResolveConcurrencyCount(ProviderGroupAccountRelation source)
    {
        if (MapContext.Current?.Parameters.TryGetValue("ConcurrencyCounts", out var countsObj) == true &&
            countsObj is IDictionary<Guid, int> counts &&
            counts.TryGetValue(source.AccountTokenId, out var count))
        {
            return count;
        }

        return 0; // 预获取已在 AppService 中完成，如果缺失则兜底返回0
    }
}

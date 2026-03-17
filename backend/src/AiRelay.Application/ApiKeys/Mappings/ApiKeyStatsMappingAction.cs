using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.UsageRecords.DomainServices;
using AutoMapper;

namespace AiRelay.Application.ApiKeys.Mappings;

public class ApiKeyStatsMappingAction(ApiKeyUsageStatisticsDomainService statisticsDomainService) : IMappingAction<ApiKey, ApiKeyOutputDto>
{
    public void Process(ApiKey source, ApiKeyOutputDto destination, ResolutionContext context)
    {
        // 1. 优先从 Context 获取批量预取的数据
        if (context.Items.TryGetValue("ApiKeyStats", out var statsObj) &&
            statsObj is Dictionary<Guid, (long UsageToday, long UsageTotal)> statsDict &&
            statsDict.TryGetValue(source.Id, out var stat))
        {
            destination.UsageToday = stat.UsageToday;
            destination.UsageTotal = stat.UsageTotal;
            return;
        }

        // 2. 兜底：自行查询 (Sync-over-Async, 仅在未传递 Context 时触发)
        var result = statisticsDomainService.GetListStatisticsAsync([source.Id]).GetAwaiter().GetResult();
        if (result.TryGetValue(source.Id, out var singleStat))
        {
            destination.UsageToday = singleStat.UsageToday;
            destination.UsageTotal = singleStat.UsageTotal;
        }
    }
}

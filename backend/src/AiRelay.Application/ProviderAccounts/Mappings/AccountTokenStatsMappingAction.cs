using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.UsageRecords.DomainServices;
using AutoMapper;

namespace AiRelay.Application.ProviderAccounts.Mappings;

public class AccountTokenStatsMappingAction(AccountUsageStatisticsDomainService statisticsDomainService) : IMappingAction<AccountToken, AccountTokenOutputDto>
{
    public void Process(AccountToken source, AccountTokenOutputDto destination, ResolutionContext context)
    {
        // 1. 优先从 Context 获取批量预取的数据
        if (context.Items.TryGetValue("AccountStats", out var statsObj) &&
            statsObj is Dictionary<Guid, (long UsageToday, long UsageTotal, decimal SuccessRate)> statsDict &&
            statsDict.TryGetValue(source.Id, out var stat))
        {
            destination.UsageToday = stat.UsageToday;
            destination.UsageTotal = stat.UsageTotal;
            destination.SuccessRate = stat.SuccessRate;
            return;
        }

        // 2. 兜底：自行查询 (Sync-over-Async, 仅在未传递 Context 时触发)
        // 注意：GetListStatisticsAsync 返回的是 Dictionary
        var result = statisticsDomainService.GetListStatisticsAsync([source.Id]).GetAwaiter().GetResult();
        if (result.TryGetValue(source.Id, out var singleStat))
        {
            destination.UsageToday = singleStat.UsageToday;
            destination.UsageTotal = singleStat.UsageTotal;
            destination.SuccessRate = singleStat.SuccessRate;
        }
    }
}

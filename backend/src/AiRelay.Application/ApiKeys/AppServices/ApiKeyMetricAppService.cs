using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.UsageRecords.DomainServices;
using Leistd.Ddd.Application.AppService;

namespace AiRelay.Application.ApiKeys.AppServices;

/// <summary>
/// ApiKey 指标应用服务
/// </summary>
public class ApiKeyMetricAppService(ApiKeyUsageStatisticsDomainService statisticsDomainService) : BaseAppService, IApiKeyMetricAppService
{
    public async Task<SubscriptionMetricsOutputDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await statisticsDomainService.GetMetricsAsync(cancellationToken);

        var result = new SubscriptionMetricsOutputDto
        {
            TotalSubscriptions = stats.TotalSubscriptions,
            ActiveSubscriptions = stats.ActiveSubscriptions,
            ExpiringSoon = stats.ExpiringSoon,
            TotalUsageToday = stats.TotalUsageToday,
            UsageGrowthRate = stats.UsageGrowthRate,
            TopUsageKeys = stats.TopUsageKeys.Select(x => new SubscriptionUsageDto
            {
                Name = x.Name,
                Usage = x.Usage
            }).ToList()
        };

        return result;
    }
}
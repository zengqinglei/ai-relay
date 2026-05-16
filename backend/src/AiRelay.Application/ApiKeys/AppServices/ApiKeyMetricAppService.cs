using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.UsageRecords.DomainServices;
using AiRelay.Domain.Users.Specifications;
using Leistd.Ddd.Application.AppService;
using Leistd.Security.Users;

namespace AiRelay.Application.ApiKeys.AppServices;

/// <summary>
/// ApiKey 指标应用服务
/// </summary>
public class ApiKeyMetricAppService(
    ApiKeyUsageStatisticsDomainService statisticsDomainService,
    ICurrentUser currentUser) : BaseAppService, IApiKeyMetricAppService
{
    public async Task<SubscriptionMetricsOutputDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var scopedUserId = UserScopeSpecifications.ResolveScopedUserId(currentUser);
        var stats = await statisticsDomainService.GetMetricsAsync(scopedUserId, cancellationToken);

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
                Usage = x.Usage,
                Unit = "次"
            }).ToList()
        };

        return result;
    }
}

using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.UsageRecords.DomainServices;
using Leistd.Ddd.Application.AppService;

namespace AiRelay.Application.ProviderAccounts.AppServices;

/// <summary>
/// 账户令牌指标应用服务
/// </summary>
public class AccountTokenMetricAppService(AccountUsageStatisticsDomainService statisticsDomainService) : BaseAppService, IAccountTokenMetricAppService
{
    public async Task<AccountTokenMetricsOutputDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await statisticsDomainService.GetMetricsAsync(cancellationToken);

        var result = new AccountTokenMetricsOutputDto
        {
            TotalAccounts = stats.TotalAccounts,
            ActiveAccounts = stats.ActiveAccounts,
            DisabledAccounts = stats.DisabledAccounts,
            ExpiringAccounts = stats.ExpiringAccounts,
            TotalUsageToday = stats.TotalUsageToday,
            UsageGrowthRate = stats.UsageGrowthRate,
            AverageSuccessRate = stats.AverageSuccessRate,
            AbnormalRequests24h = stats.AbnormalRequests24h,
            RotationWarnings = stats.ExpiringAccounts
        };

        return result;
    }
}
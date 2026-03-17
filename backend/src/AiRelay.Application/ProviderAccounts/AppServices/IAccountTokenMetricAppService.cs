using AiRelay.Application.ProviderAccounts.Dtos;
using Leistd.Ddd.Application.Contracts.AppService;

namespace AiRelay.Application.ProviderAccounts.AppServices;

/// <summary>
/// 账户令牌指标应用服务接口
/// </summary>
public interface IAccountTokenMetricAppService : IAppService
{
    /// <summary>
    /// 获取账户指标统计
    /// </summary>
    Task<AccountTokenMetricsOutputDto> GetMetricsAsync(CancellationToken cancellationToken = default);
}

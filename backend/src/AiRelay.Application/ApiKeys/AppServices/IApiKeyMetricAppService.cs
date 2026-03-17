using AiRelay.Application.ApiKeys.Dtos;
using Leistd.Ddd.Application.Contracts.AppService;

namespace AiRelay.Application.ApiKeys.AppServices;

/// <summary>
/// ApiKey 指标应用服务接口
/// </summary>
public interface IApiKeyMetricAppService : IAppService
{
    /// <summary>
    /// 获取 ApiKey 指标统计
    /// </summary>
    Task<SubscriptionMetricsOutputDto> GetMetricsAsync(CancellationToken cancellationToken = default);
}

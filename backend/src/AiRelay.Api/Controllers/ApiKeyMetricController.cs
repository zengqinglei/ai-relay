using AiRelay.Application.ApiKeys.AppServices;
using AiRelay.Application.ApiKeys.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// ApiKey 指标控制器
/// </summary>
[Authorize]
[Route("api/v1/api-keys/metrics")]
public class ApiKeyMetricController(IApiKeyMetricAppService apiKeyMetricAppService) : BaseController
{
    /// <summary>
    /// 获取 ApiKey 指标统计
    /// </summary>
    [HttpGet]
    public async Task<SubscriptionMetricsOutputDto> GetMetricsAsync(CancellationToken cancellationToken)
    {
        return await apiKeyMetricAppService.GetMetricsAsync(cancellationToken);
    }
}

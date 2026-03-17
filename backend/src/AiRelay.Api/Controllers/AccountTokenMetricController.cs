using AiRelay.Application.ProviderAccounts.AppServices;
using AiRelay.Application.ProviderAccounts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 账户令牌指标控制器
/// </summary>
[Authorize]
[Route("api/v1/account-tokens/metrics")]
public class AccountTokenMetricController(IAccountTokenMetricAppService accountTokenMetricAppService) : BaseController
{
    /// <summary>
    /// 获取账户指标统计
    /// </summary>
    [HttpGet]
    public async Task<AccountTokenMetricsOutputDto> GetMetricsAsync(
        CancellationToken cancellationToken)
    {
        return await accountTokenMetricAppService.GetMetricsAsync(cancellationToken);
    }
}

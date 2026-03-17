using AiRelay.Application.UsageRecords.AppServices;
using AiRelay.Application.UsageRecords.Dtos.Query;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 使用记录指标控制器
/// </summary>
[Authorize]
[Route("api/v1/usage/query")]
public class UsageRecordMetricController(IUsageRecordMetricAppService usageRecordMetricAppService) : BaseController
{
    /// <summary>
    /// 获取流量指标 (请求数, RPS, 趋势, 今日消耗)
    /// </summary>
    [HttpGet("metrics")]
    public async Task<UsageMetricsOutputDto> GetMetricsAsync([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime, CancellationToken cancellationToken)
    {
        return await usageRecordMetricAppService.GetMetricsAsync(startTime, endTime, cancellationToken);
    }

    /// <summary>
    /// 获取流量趋势 (含Token)
    /// </summary>
    [HttpGet("trend")]
    public async Task<List<UsageTrendOutputDto>> GetTrendAsync([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime, CancellationToken cancellationToken)
    {
        return await usageRecordMetricAppService.GetTrendAsync(startTime, endTime, cancellationToken);
    }

    /// <summary>
    /// 获取 Top 7 API Key 使用趋势
    /// </summary>
    [HttpGet("top-api-keys")]
    public async Task<List<ApiKeyTrendOutputDto>> GetTopApiKeyTrendAsync([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime, CancellationToken cancellationToken)
    {
        return await usageRecordMetricAppService.GetTopApiKeyTrendAsync(startTime, endTime, cancellationToken);
    }

    /// <summary>
    /// 获取模型使用分布（TOP 7）
    /// </summary>
    [HttpGet("model-distribution")]
    public async Task<List<ModelDistributionOutputDto>> GetModelDistributionAsync([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime, CancellationToken cancellationToken)
    {
        return await usageRecordMetricAppService.GetModelDistributionAsync(startTime, endTime, cancellationToken);
    }
}
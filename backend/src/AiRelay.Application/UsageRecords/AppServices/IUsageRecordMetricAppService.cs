using AiRelay.Application.UsageRecords.Dtos.Query;

namespace AiRelay.Application.UsageRecords.AppServices;

/// <summary>
/// 使用记录指标应用服务接口
/// </summary>
public interface IUsageRecordMetricAppService
{
    /// <summary>
    /// 获取聚合指标 (Total, Trend, RPS, Consumption)
    /// </summary>
    Task<UsageMetricsOutputDto> GetMetricsAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取使用趋势图表数据 (Request + Token)
    /// </summary>
    Task<List<UsageTrendOutputDto>> GetTrendAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 Top 10 API Key 使用趋势
    /// </summary>
    Task<List<ApiKeyTrendOutputDto>> GetTopApiKeyTrendAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取模型使用分布（TOP 10）
    /// </summary>
    Task<List<ModelDistributionOutputDto>> GetModelDistributionAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default);
}
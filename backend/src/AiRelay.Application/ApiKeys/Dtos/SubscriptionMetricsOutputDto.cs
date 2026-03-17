namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// 订阅（ApiKey）统计指标输出 DTO
/// </summary>
public record SubscriptionMetricsOutputDto
{
    /// <summary>
    /// 总订阅数
    /// </summary>
    public long TotalSubscriptions { get; init; }

    /// <summary>
    /// 活跃订阅数
    /// </summary>
    public long ActiveSubscriptions { get; init; }

    /// <summary>
    /// 即将过期数量（7天内）
    /// </summary>
    public long ExpiringSoon { get; init; }

    /// <summary>
    /// 今日总用量
    /// </summary>
    public long TotalUsageToday { get; init; }

    /// <summary>
    /// 用量增长率
    /// </summary>
    public decimal UsageGrowthRate { get; init; }

    /// <summary>
    /// 用量 Top N
    /// </summary>
    public List<SubscriptionUsageDto> TopUsageKeys { get; init; } = [];
}

/// <summary>
/// 订阅用量排行项
/// </summary>
public record SubscriptionUsageDto
{
    /// <summary>
    /// 订阅名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 用量
    /// </summary>
    public long Usage { get; init; }
}

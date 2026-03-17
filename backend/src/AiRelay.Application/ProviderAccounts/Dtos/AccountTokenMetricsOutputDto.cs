namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 提供商账户指标输出 DTO
/// </summary>
public class AccountTokenMetricsOutputDto
{
    /// <summary>
    /// 账户总数
    /// </summary>
    public int TotalAccounts { get; init; }

    /// <summary>
    /// 启用的账户数
    /// </summary>
    public int ActiveAccounts { get; init; }

    /// <summary>
    /// 禁用的账户数
    /// </summary>
    public int DisabledAccounts { get; init; }

    /// <summary>
    /// 即将过期的账户数
    /// </summary>
    public int ExpiringAccounts { get; init; }

    /// <summary>
    /// 今日总使用次数
    /// </summary>
    public long TotalUsageToday { get; init; }

    /// <summary>
    /// 使用增长率（较昨日）
    /// </summary>
    public decimal UsageGrowthRate { get; init; }

    /// <summary>
    /// 平均成功率
    /// </summary>
    public decimal AverageSuccessRate { get; init; }

    /// <summary>
    /// 24小时异常请求数
    /// </summary>
    public long AbnormalRequests24h { get; init; }

    /// <summary>
    /// 轮换预警数量
    /// </summary>
    public int RotationWarnings { get; init; }
}

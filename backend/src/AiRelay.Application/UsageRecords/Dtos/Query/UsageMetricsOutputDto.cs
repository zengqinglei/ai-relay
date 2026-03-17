namespace AiRelay.Application.UsageRecords.Dtos.Query;

/// <summary>
/// 流量指标输出
/// </summary>
public class UsageMetricsOutputDto
{
    /// <summary>
    /// 今日总请求数
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// 请求数趋势 (百分比)
    /// </summary>
    public decimal RequestsTrend { get; set; }

    /// <summary>
    /// 当前 RPS
    /// </summary>
    public decimal CurrentRps { get; set; }

    /// <summary>
    /// 今日输入 Token 总数
    /// </summary>
    public long TotalInputTokens { get; set; }

    /// <summary>
    /// 今日输出 Token 总数
    /// </summary>
    public long TotalOutputTokens { get; set; }

    /// <summary>
    /// 今日总消耗金额
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// 今日成功请求数
    /// </summary>
    public int SuccessRequests { get; set; }

    /// <summary>
    /// 今日失败请求数
    /// </summary>
    public int FailedRequests { get; set; }
}

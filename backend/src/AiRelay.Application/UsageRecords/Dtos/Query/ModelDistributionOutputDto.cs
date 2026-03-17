namespace AiRelay.Application.UsageRecords.Dtos.Query;

/// <summary>
/// 模型使用分布输出
/// </summary>
public class ModelDistributionOutputDto
{
    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 请求数
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// 总 Token 数（输入 + 输出）
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// 总消耗金额
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// 占比（百分比）
    /// </summary>
    public decimal Percentage { get; set; }
}

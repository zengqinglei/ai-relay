namespace AiRelay.Application.UsageRecords.Dtos.Query;

/// <summary>
/// 流量趋势输出
/// </summary>
public class UsageTrendOutputDto
{
    /// <summary>
    /// 时间点 (HH:mm)
    /// </summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// 请求数
    /// </summary>
    public int Requests { get; set; }

    /// <summary>
    /// 输入 Token
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// 输出 Token
    /// </summary>
    public long OutputTokens { get; set; }
}

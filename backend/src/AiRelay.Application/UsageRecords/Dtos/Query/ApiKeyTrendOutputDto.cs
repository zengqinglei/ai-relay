namespace AiRelay.Application.UsageRecords.Dtos.Query;

/// <summary>
/// API Key 使用趋势输出
/// </summary>
public class ApiKeyTrendOutputDto
{
    /// <summary>
    /// API Key 名称
    /// </summary>
    public string ApiKeyName { get; set; } = string.Empty;

    /// <summary>
    /// 趋势数据点
    /// </summary>
    public List<UsageTrendOutputDto> Trend { get; set; } = [];

    /// <summary>
    /// 总请求数（用于排序）
    /// </summary>
    public int TotalRequests { get; set; }
}

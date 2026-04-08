namespace AiRelay.Application.UsageRecords.Dtos.Query;

/// <summary>
/// 趋势数据内部聚合传输对象
/// </summary>
public class TrendDataDto
{
    public DateTime Date { get; set; }
    public int Hour { get; set; }
    public int Requests { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}

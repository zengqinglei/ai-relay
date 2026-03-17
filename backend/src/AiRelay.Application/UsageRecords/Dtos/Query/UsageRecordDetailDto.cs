namespace AiRelay.Application.UsageRecords.Dtos.Query;

public class UsageRecordDetailOutputDto
{
    public Guid UsageRecordId { get; set; }
    public string? DownRequestUrl { get; set; }
    public string? DownRequestHeaders { get; set; }
    public string? DownRequestBody { get; set; }
    public string? DownResponseBody { get; set; }
    public string? UpRequestUrl { get; set; }
    public string? UpRequestHeaders { get; set; }
    public string? UpRequestBody { get; set; }
    public string? UpResponseBody { get; set; }
    public int? UpStatusCode { get; set; }
}

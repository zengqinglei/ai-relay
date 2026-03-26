namespace AiRelay.Application.UsageRecords.Dtos.Query;

public class UsageRecordDetailOutputDto
{
    public Guid UsageRecordId { get; set; }
    public string? DownRequestUrl { get; set; }
    public string? DownRequestHeaders { get; set; }
    public string? DownRequestBody { get; set; }
    public string? DownResponseBody { get; set; }
    public List<UsageRecordAttemptOutputDto> Attempts { get; set; } = [];
}

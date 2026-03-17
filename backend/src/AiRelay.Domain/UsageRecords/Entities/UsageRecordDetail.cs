using Leistd.Ddd.Domain.Entities;

namespace AiRelay.Domain.UsageRecords.Entities;

/// <summary>
/// Token 使用记录详情 (大字段存储)
/// </summary>
public class UsageRecordDetail : Entity<Guid>
{
    public Guid UsageRecordId { get; private set; }

    public string? DownRequestHeaders { get; private set; }

    public string? DownRequestBody { get; private set; }

    public string? DownResponseBody { get; private set; }

    public string? UpRequestHeaders { get; private set; }

    public string? UpRequestBody { get; private set; }

    public string? UpResponseBody { get; private set; }

    internal UsageRecordDetail(Guid usageRecordId,
        string? downRequestHeaders,
        string? downRequestBody,
        string? upRequestHeaders,
        string? upRequestBody)
    {
        Id = Guid.CreateVersion7();
        UsageRecordId = usageRecordId;
        DownRequestHeaders = downRequestHeaders;
        DownRequestBody = downRequestBody;
        UpRequestHeaders = upRequestHeaders;
        UpRequestBody = upRequestBody;
    }

    internal void Complete(string? upResponseBody, string? downResponseBody)
    {
        UpResponseBody = upResponseBody;
        DownResponseBody = downResponseBody;
    }

    private UsageRecordDetail() { }
}

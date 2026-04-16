using Leistd.Ddd.Domain.Entities;

namespace AiRelay.Domain.UsageRecords.Entities;

/// <summary>
/// Token 使用记录详情（下游大字段存储，1:1 with UsageRecord）
/// </summary>
public class UsageRecordDetail : Entity<Guid>
{
    public Guid UsageRecordId { get; private set; }

    public string? DownRequestHeaders { get; private set; }

    public string? DownRequestBody { get; private set; }

    public string? DownResponseBody { get; private set; }

    internal UsageRecordDetail(
        Guid usageRecordId,
        string? downRequestHeaders,
        string? downRequestBody)
    {
        Id = Guid.CreateVersion7();
        UsageRecordId = usageRecordId;
        DownRequestHeaders = downRequestHeaders;
        DownRequestBody = downRequestBody;
    }

    internal void Complete(string? downResponseBody, string? downRequestHeaders = null, string? downRequestBody = null)
    {
        DownResponseBody = downResponseBody;
        if (downRequestHeaders != null) DownRequestHeaders = downRequestHeaders;
        if (downRequestBody != null) DownRequestBody = downRequestBody;
    }

    private UsageRecordDetail() { }
}

using Leistd.Ddd.Domain.Entities;

namespace AiRelay.Domain.UsageRecords.Entities;

/// <summary>
/// 上游尝试大字段详情（1:1 with UsageRecordAttempt）
/// </summary>
public class UsageRecordAttemptDetail : Entity<Guid>
{
    public Guid UsageRecordAttemptId { get; private set; }

    public string? UpRequestHeaders { get; private set; }

    public string? UpRequestBody { get; private set; }

    public string? UpResponseBody { get; private set; }

    internal UsageRecordAttemptDetail(
        Guid usageRecordAttemptId,
        string? upRequestHeaders,
        string? upRequestBody,
        string? upResponseBody)
    {
        Id = Guid.CreateVersion7();
        UsageRecordAttemptId = usageRecordAttemptId;
        UpRequestHeaders = upRequestHeaders;
        UpRequestBody = upRequestBody;
        UpResponseBody = upResponseBody;
    }

    private UsageRecordAttemptDetail() { }
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Domain.Entities;

namespace AiRelay.Domain.UsageRecords.Entities;

/// <summary>
/// 单次上游尝试记录（1:N with UsageRecord）
/// </summary>
public class UsageRecordAttempt : Entity<Guid>
{
    public Guid UsageRecordId { get; private set; }

    public int AttemptNumber { get; private set; }

    public Guid AccountTokenId { get; private set; }

    public string AccountTokenName { get; private set; }

    public string? UpModelId { get; private set; }

    public string? UpUserAgent { get; private set; }

    public string? UpRequestUrl { get; private set; }

    public int? UpStatusCode { get; private set; }

    public long DurationMs { get; private set; }

    public UsageStatus Status { get; private set; }

    public string? StatusDescription { get; private set; }

    public UsageRecordAttemptDetail Detail { get; private set; } = null!;

    public UsageRecordAttempt(
        Guid usageRecordId,
        int attemptNumber,
        Guid accountTokenId,
        string accountTokenName,
        string? upModelId,
        string? upUserAgent,
        string? upRequestUrl,
        string? upRequestHeaders,
        string? upRequestBody,
        string? upResponseBody,
        int? upStatusCode,
        long durationMs,
        UsageStatus status,
        string? statusDescription)
    {
        Id = Guid.CreateVersion7();
        UsageRecordId = usageRecordId;
        AttemptNumber = attemptNumber;
        AccountTokenId = accountTokenId;
        AccountTokenName = accountTokenName;
        UpModelId = upModelId;
        UpUserAgent = upUserAgent;
        UpRequestUrl = upRequestUrl;
        UpStatusCode = upStatusCode;
        DurationMs = durationMs;
        Status = status;
        StatusDescription = statusDescription?.Length > 2048
            ? statusDescription[..2045] + "..."
            : statusDescription;
        Detail = new UsageRecordAttemptDetail(Id, upRequestHeaders, upRequestBody, upResponseBody);
    }

    private UsageRecordAttempt() { AccountTokenName = null!; }
}

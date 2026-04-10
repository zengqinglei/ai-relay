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

    public Guid? ProviderGroupId { get; private set; }

    public string? ProviderGroupName { get; private set; }

    public decimal? GroupRateMultiplier { get; private set; }

    public string? UpModelId { get; private set; }

    public string? UpUserAgent { get; private set; }

    public string? UpRequestUrl { get; private set; }

    public int? UpStatusCode { get; private set; }

    public long DurationMs { get; private set; }

    public UsageStatus Status { get; private set; }

    public Provider Provider { get; private set; }

    public AuthMethod AuthMethod { get; private set; }

    public string? StatusDescription { get; private set; }

    public UsageRecordAttemptDetail Detail { get; private set; } = null!;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; private set; }
    
    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime EndTime { get; private set; }

    /// <summary>
    /// 创建进行中的尝试记录（选号成功后立即调用）
    /// </summary>
    public UsageRecordAttempt(
        Guid usageRecordId,
        int attemptNumber,
        Guid accountTokenId,
        string accountTokenName,
        Provider provider,
        AuthMethod authMethod,
        Guid? providerGroupId,
        string? providerGroupName,
        decimal? groupRateMultiplier,
        string? upModelId,
        string? upUserAgent,
        string? upRequestUrl,
        string? upRequestHeaders,
        string? upRequestBody)
    {
        Id = Guid.CreateVersion7();
        UsageRecordId = usageRecordId;
        AttemptNumber = attemptNumber;
        AccountTokenId = accountTokenId;
        AccountTokenName = accountTokenName;
        Provider = provider;
        AuthMethod = authMethod;
        ProviderGroupId = providerGroupId;
        ProviderGroupName = providerGroupName;
        GroupRateMultiplier = groupRateMultiplier;
        UpModelId = upModelId;
        UpUserAgent = upUserAgent;
        UpRequestUrl = upRequestUrl;
        Status = UsageStatus.InProgress;
        StartTime = DateTime.UtcNow;
        Detail = new UsageRecordAttemptDetail(Id, upRequestHeaders, upRequestBody, null);
    }

    /// <summary>
    /// 完成尝试记录（HTTP 请求结束后调用）
    /// </summary>
    public void Complete(
        int? upStatusCode,
        long durationMs,
        UsageStatus status,
        string? statusDescription,
        string? upResponseBody)
    {
        UpStatusCode = upStatusCode;
        DurationMs = durationMs;
        Status = status;
        StatusDescription = statusDescription?.Length > 2048
            ? statusDescription[..2045] + "..."
            : statusDescription;
        EndTime = DateTime.UtcNow;
        Detail.CompleteAttempt(upResponseBody);
    }

    private UsageRecordAttempt() { AccountTokenName = null!; }
}

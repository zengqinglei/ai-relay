using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.UsageRecords.Entities;

/// <summary>
/// Token 使用记录实体（客户端请求维度，1条/请求）
/// </summary>
public class UsageRecord : CreationAuditedEntity<Guid>
{
    public string? SessionId { get; private set; }

    public string CorrelationId { get; private set; }

    public Guid ApiKeyId { get; private set; }

    public string ApiKeyName { get; private set; }

    public bool IsStreaming { get; private set; }

    public string DownRequestMethod { get; private set; }

    public string DownRequestUrl { get; private set; }

    public string? DownModelId { get; private set; }

    public string? DownClientIp { get; private set; }

    public string? DownUserAgent { get; private set; }

    public UsageStatus Status { get; private set; }

    public string? StatusDescription { get; private set; }

    public long? DurationMs { get; private set; }

    public int? InputTokens { get; private set; }

    public int? OutputTokens { get; private set; }

    public int? CacheReadTokens { get; private set; }

    public int? CacheCreationTokens { get; private set; }

    public decimal? BaseCost { get; private set; }

    public decimal? FinalCost { get; private set; }

    public int AttemptCount { get; private set; }

    /// <summary>返回给下游客户端的 HTTP 状态码</summary>
    public int? DownStatusCode { get; private set; }

    // 导航属性
    public ApiKey ApiKey { get; private set; } = null!;
    public UsageRecordDetail Detail { get; private set; } = null!;

    private readonly List<UsageRecordAttempt> _attempts = [];
    public IReadOnlyList<UsageRecordAttempt> Attempts => _attempts.AsReadOnly();

    public UsageRecord(
        Guid usageRecordId,
        string correlationId,
        string? sessionId,
        Guid apiKeyId,
        string apiKeyName,
        bool isStreaming,
        string downRequestMethod,
        string downRequestUrl,
        string? downModelId,
        string? downClientIp,
        string? downUserAgent,
        string? downRequestHeaders,
        string? downRequestBody)
    {
        Id = usageRecordId;
        CorrelationId = correlationId;
        SessionId = sessionId;
        ApiKeyId = apiKeyId;
        ApiKeyName = apiKeyName;
        IsStreaming = isStreaming;
        DownModelId = downModelId;
        DownRequestMethod = downRequestMethod;
        DownRequestUrl = downRequestUrl;
        DownClientIp = downClientIp;
        DownUserAgent = downUserAgent;
        Detail = new UsageRecordDetail(Id, downRequestHeaders, downRequestBody);
        Status = UsageStatus.InProgress;
    }

    public void Complete(
        decimal groupRateMultiplier,
        long duration,
        UsageStatus status,
        string? statusDescription,
        string? downResponseBody,
        int? inputTokens,
        int? outputTokens,
        int? cacheReadTokens,
        int? cacheCreationTokens,
        decimal? baseCost,
        int attemptCount,
        int? downStatusCode)
    {
        DurationMs = duration;
        Status = status;
        StatusDescription = statusDescription?.Length > 2048
            ? statusDescription[..2045] + "..."
            : statusDescription;
        Detail.Complete(downResponseBody);
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CacheReadTokens = cacheReadTokens;
        CacheCreationTokens = cacheCreationTokens;
        AttemptCount = attemptCount;
        DownStatusCode = downStatusCode;
        if (baseCost.HasValue)
        {
            BaseCost = baseCost;
            FinalCost = BaseCost.Value * groupRateMultiplier;
        }
    }

    private UsageRecord()
    {
        CorrelationId = null!;
        ApiKeyName = null!;
        DownRequestMethod = null!;
        DownRequestUrl = null!;
    }
}

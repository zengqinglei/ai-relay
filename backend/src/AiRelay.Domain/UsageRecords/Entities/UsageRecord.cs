using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.UsageRecords.Entities;

/// <summary>
/// Token 使用记录实体
/// </summary>
public class UsageRecord : CreationAuditedEntity<Guid>
{
    public ProviderPlatform Platform { get; private set; }

    public string CorrelationId { get; private set; }

    public Guid ApiKeyId { get; private set; }

    public string ApiKeyName { get; private set; }

    public Guid AccountTokenId { get; private set; }

    public string AccountTokenName { get; private set; }

    public Guid ProviderGroupId { get; private set; }

    public string ProviderGroupName { get; private set; }

    public decimal GroupRateMultiplier { get; private set; }

    public bool IsStreaming { get; private set; }

    public string DownRequestMethod { get; private set; }

    public string DownRequestUrl { get; private set; }

    public string? DownModelId { get; private set; }

    public string? DownClientIp { get; private set; }

    public string? DownUserAgent { get; private set; }

    public string? UpModelId { get; private set; }

    public string? UpUserAgent { get; private set; }

    public string? UpRequestUrl { get; private set; }

    public int? UpStatusCode { get; private set; }

    public UsageStatus Status { get; private set; }

    public string? StatusDescription { get; private set; }

    public long? DurationMs { get; private set; }

    public int? InputTokens { get; private set; }

    public int? OutputTokens { get; private set; }

    public int? CacheReadTokens { get; private set; }

    public int? CacheCreationTokens { get; private set; }

    public decimal? BaseCost { get; private set; }

    public decimal? FinalCost { get; private set; }

    // 导航属性
    public ApiKey ApiKey { get; private set; } = null!;
    public AccountToken? AccountToken { get; private set; }
    public UsageRecordDetail Detail { get; private set; } = null!;

    public UsageRecord(
        Guid usageRecordId,
        string correlationId,
        ProviderPlatform platform,
        Guid apiKeyId,
        string apiKeyName,
        Guid accountTokenId,
        string accountTokenName,
        Guid providerGroupId,
        string providerGroupName,
        decimal groupRateMultiplier,
        bool isStreaming,
        string downRequestMethod,
        string downRequestUrl,
        string? downModelId,
        string? downClientIp,
        string? downUserAgent,
        string? downRequestHeaders,
        string? downRequestBody,
        string? upModelId,
        string? upRequestUrl,
        string? upUserAgent,
        string? upRequestHeaders,
        string? upRequestBody)
    {
        Id = usageRecordId;
        CorrelationId = correlationId;
        Platform = platform;
        ApiKeyId = apiKeyId;
        ProviderGroupId = providerGroupId;
        ProviderGroupName = providerGroupName;
        GroupRateMultiplier = groupRateMultiplier;
        ApiKeyName = apiKeyName;
        AccountTokenId = accountTokenId;
        AccountTokenName = accountTokenName;
        IsStreaming = isStreaming;
        DownModelId = downModelId;
        DownRequestMethod = downRequestMethod;
        DownRequestUrl = downRequestUrl;
        DownClientIp = downClientIp;
        DownUserAgent = downUserAgent;
        UpModelId = upModelId;
        UpUserAgent = upUserAgent;
        UpRequestUrl = upRequestUrl;
        Detail = new UsageRecordDetail(
            Id,
            downRequestHeaders: downRequestHeaders,
            downRequestBody: downRequestBody,
            upRequestHeaders,
            upRequestBody);

        Status = UsageStatus.InProgress;
    }

    public void Complete(
        long duration,
        int? upStatusCode,
        UsageStatus status,
        string? statusDescription,
        string? upResponseBody,
        string? downResponseBody,
        int? inputTokens,
        int? outputTokens,
        int? cacheReadTokens,
        int? cacheCreationTokens,
        decimal? baseCost)
    {
        DurationMs = duration;
        UpStatusCode = upStatusCode;
        Status = status;
        StatusDescription = statusDescription?.Length > 2048
            ? statusDescription.Substring(0, 2045) + "..."
            : statusDescription;
        Detail.Complete(upResponseBody, downResponseBody);

        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CacheReadTokens = cacheReadTokens;
        CacheCreationTokens = cacheCreationTokens;
        if (baseCost.HasValue)
        {
            BaseCost = baseCost;
            FinalCost = BaseCost.Value * GroupRateMultiplier;
        }
    }

    private UsageRecord()
    {
        CorrelationId = null!;
        ApiKeyName = null!;
        AccountTokenName = null!;
        ProviderGroupName = null!;
        DownRequestMethod = null!;
        DownRequestUrl = null!;
    }
}

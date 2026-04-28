using System;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.ValueObjects;

namespace AiRelay.Application.UsageRecords.Queue;

/// <summary>
/// 统一的使用记录异步落库队列接口
/// </summary>
public interface IUsageRecordQueue
{
    /// <summary>
    /// 添加使用记录到队列（非阻塞）
    /// </summary>
    bool TryEnqueue(IUsageRecordItem item);
}

/// <summary>
/// 使用记录项基础接口
/// </summary>
public interface IUsageRecordItem
{
    Guid UsageRecordId { get; }
}

/// <summary>
/// 使用记录开始项（INSERT UsageRecord，Status=InProgress）
/// </summary>
public record UsageRecordStartItem(
    Guid UsageRecordId,
    Guid UserId,
    UsageSource Source,
    string CorrelationId,
    string? SessionId,
    Guid? ApiKeyId,
    string? ApiKeyName,
    bool IsStreaming,
    string DownRequestMethod,
    string DownRequestUrl,
    string? DownModelId,
    string? DownClientIp,
    string? DownUserAgent,
    string? DownRequestHeaders,
    string? DownRequestBody
) : IUsageRecordItem;

/// <summary>
/// 单次上游尝试开始项（INSERT UsageRecordAttempt，Status=InProgress，选号后立即入队）
/// </summary>
public record UsageRecordAttemptStartItem(
    Guid UsageRecordId,
    int AttemptNumber,
    Guid AccountTokenId,
    string AccountTokenName,
    Provider Provider,
    AuthMethod AuthMethod,
    Guid? ProviderGroupId,
    string? ProviderGroupName,
    decimal? GroupRateMultiplier,
    string? UpModelId,
    string? UpUserAgent,
    string? UpRequestUrl,
    string? UpRequestHeaders,
    string? UpRequestBody
) : IUsageRecordItem;

/// <summary>
/// 单次上游尝试结束项（UPDATE UsageRecordAttempt 为最终状态，HTTP 请求结束后入队）
/// </summary>
public record UsageRecordAttemptEndItem(
    Guid UsageRecordId,
    int AttemptNumber,
    int? UpStatusCode,
    long DurationMs,
    UsageStatus Status,
    string? StatusDescription,
    string? UpResponseBody,
    string? UpRequestHeaders = null,
    string? UpRequestBody = null
) : IUsageRecordItem;

/// <summary>
/// 使用记录结束项（UPDATE UsageRecord 为最终状态）
/// </summary>
public record UsageRecordEndItem(
    Guid UsageRecordId,
    long Duration,
    UsageStatus Status,
    string? StatusDescription,
    string? DownResponseBody,
    int? InputTokens,
    int? OutputTokens,
    int? CacheReadTokens,
    int? CacheCreationTokens,
    int AttemptCount,
    int? DownStatusCode,
    string? DownRequestHeaders = null,
    string? DownRequestBody = null
) : IUsageRecordItem;

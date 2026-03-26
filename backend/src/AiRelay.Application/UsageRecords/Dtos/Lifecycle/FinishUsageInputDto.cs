using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record FinishUsageInputDto(
    Guid UsageRecordId,
    // 响应状态
    long Duration,
    UsageStatus Status,
    string? StatusDescription,
    // 响应内容
    string? DownResponseBody,
    // 计费信息（TOKEN）
    int? InputTokens,
    int? OutputTokens,
    int? CacheReadTokens,
    int? CacheCreationTokens,
    // 尝试次数
    int AttemptCount,
    // 最终成功尝试的账号信息（用于定价和缓存统计）
    string? UpModelId,
    Guid? AccountTokenId
);

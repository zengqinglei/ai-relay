using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record FinishUsageInputDto(
    Guid UsageRecordId,
    // 响应状态
    long Duration,
    int? UpStatusCode,
    UsageStatus Status,
    string? StatusDescription,
    // 响应内容
    string? UpResponseBody,
    string? DownResponseBody,
    // 计费信息（TOKEN）
    int? InputTokens,
    int? OutputTokens,
    int? CacheReadTokens,
    int? CacheCreationTokens
);
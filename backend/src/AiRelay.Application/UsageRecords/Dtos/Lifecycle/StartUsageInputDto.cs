using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record StartUsageInputDto(
    Guid UsageRecordId,
    string CorrelationId,
    ProviderPlatform Platform,
    Guid ApiKeyId,
    string ApiKeyName,
    Guid AccountTokenId,
    string AccountTokenName,
    Guid ProviderGroupId,
    string ProviderGroupName,
    decimal GroupRateMultiplier,
    // 下游请求信息
    bool IsStreaming,
    string DownRequestMethod,
    string DownRequestUrl,
    string? DownModelId,
    string? DownClientIp,
    string? DownUserAgent,
    string? DownRequestHeaders,
    string? DownRequestBody,
    // 转发上游信息
    string? UpModelId,
    string? UpRequestUrl,
    string? UpUserAgent,
    string? UpRequestHeaders,
    string? UpRequestBody
);

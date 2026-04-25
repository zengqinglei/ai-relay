using AiRelay.Domain.UsageRecords.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record StartUsageInputDto(
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
);


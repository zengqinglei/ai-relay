using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record AddAttemptInputDto(
    Guid UsageRecordId,
    int AttemptNumber,
    Guid AccountTokenId,
    string AccountTokenName,
    string? UpModelId,
    string? UpUserAgent,
    string? UpRequestUrl,
    string? UpRequestHeaders,
    string? UpRequestBody,
    string? UpResponseBody,
    int? UpStatusCode,
    long DurationMs,
    UsageStatus Status,
    string? StatusDescription
);

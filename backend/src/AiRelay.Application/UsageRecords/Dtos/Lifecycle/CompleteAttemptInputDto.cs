using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record CompleteAttemptInputDto(
    Guid UsageRecordId,
    int AttemptNumber,
    int? UpStatusCode,
    long DurationMs,
    UsageStatus Status,
    string? StatusDescription,
    string? UpResponseBody,
    string? UpRequestHeaders = null,
    string? UpRequestBody = null
);

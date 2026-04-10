using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record StartAttemptInputDto(
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
);

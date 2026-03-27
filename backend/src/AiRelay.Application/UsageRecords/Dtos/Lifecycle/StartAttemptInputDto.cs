namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record StartAttemptInputDto(
    Guid UsageRecordId,
    int AttemptNumber,
    Guid AccountTokenId,
    string AccountTokenName,
    Guid? ProviderGroupId,
    string? ProviderGroupName,
    decimal? GroupRateMultiplier,
    string? UpModelId,
    string? UpUserAgent,
    string? UpRequestUrl,
    string? UpRequestHeaders,
    string? UpRequestBody
);

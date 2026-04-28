using AiRelay.Domain.UsageRecords.ValueObjects;

namespace AiRelay.Application.ModelRoutes.Dtos;

public record RouteExecutionMetadata(
    Guid UsageRecordId,
    Guid UserId,
    UsageSource Source,
    string CorrelationId,
    Guid? ApiKeyId,
    string? ApiKeyName
);

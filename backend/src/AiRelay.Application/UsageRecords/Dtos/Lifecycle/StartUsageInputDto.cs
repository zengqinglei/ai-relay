namespace AiRelay.Application.UsageRecords.Dtos.Lifecycle;

public record StartUsageInputDto(
    Guid UsageRecordId,
    string CorrelationId,
    string? SessionId,
    Guid ApiKeyId,
    string ApiKeyName,
    // 下游请求信息
    bool IsStreaming,
    string DownRequestMethod,
    string DownRequestUrl,
    string? DownModelId,
    string? DownClientIp,
    string? DownUserAgent,
    string? DownRequestHeaders,
    string? DownRequestBody
);

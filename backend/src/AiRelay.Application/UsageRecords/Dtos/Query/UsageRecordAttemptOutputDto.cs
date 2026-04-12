using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.UsageRecords.Dtos.Query;

public class UsageRecordAttemptOutputDto
{
    public int AttemptNumber { get; set; }

    public string AccountTokenName { get; set; } = string.Empty;

    public string? UpModelId { get; set; }

    public string? UpUserAgent { get; set; }

    public string? UpRequestUrl { get; set; }

    public int? UpStatusCode { get; set; }

    public long DurationMs { get; set; }

    public UsageStatus Status { get; set; }

    public string? StatusDescription { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public Provider Provider { get; set; }

    public AuthMethod AuthMethod { get; set; }

    public string? UpRequestHeaders { get; set; }

    public string? UpRequestBody { get; set; }

    public string? UpResponseBody { get; set; }
}

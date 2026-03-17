using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ProviderAccounts.Dtos;

public record AvailableAccountTokenOutputDto
{
    public required Guid Id { get; init; }
    public required ProviderPlatform Platform { get; init; }
    public required string Name { get; init; }
    public Dictionary<string, string> ExtraProperties { get; init; } = new();
    public required string AccessToken { get; init; }
    public string? BaseUrl { get; init; }
    public int MaxConcurrency { get; init; }
    public int CurrentConcurrency { get; init; }
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ProviderGroups.Dtos;

public record SelectProxyAccountInputDto
{
    [Required]
    public ProviderPlatform Platform { get; init; }

    [Required]
    public Guid ApiKeyId { get; init; }

    public string? ApiKeyName { get; init; }

    public string? SessionHash { get; init; }

    public IEnumerable<Guid>? ExcludedAccountIds { get; init; }

    public string? ModelId { get; init; }
}


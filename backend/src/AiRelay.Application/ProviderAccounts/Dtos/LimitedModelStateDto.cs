namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 模型级限流状态 DTO
/// </summary>
public class LimitedModelStateDto
{
    public string ModelKey { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public DateTime? LockedUntil { get; init; }

    public string? StatusDescription { get; init; }
}

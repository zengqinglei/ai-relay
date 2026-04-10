using AiRelay.Domain.ProviderAccounts.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ProviderGroups.Dtos;

public record SelectProxyAccountInputDto
{
    [Required]
    public Guid ApiKeyId { get; init; }

    public string? ApiKeyName { get; init; }

    public string? SessionHash { get; init; }

    public IEnumerable<Guid>? ExcludedAccountIds { get; init; }

    public string? ModelId { get; init; }

    /// <summary>
    /// 当前路由端点允许的 (Provider, AuthMethod) 组合集合。
    /// 从 RouteProfile → RouteProfileRegistry 解析而来，不为 null 时将过滤不支持的账号。
    /// </summary>
    public IReadOnlyList<(Provider Provider, AuthMethod AuthMethod)>? AllowedCombinations { get; init; }
}

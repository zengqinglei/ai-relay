using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 调度阶段使用的可用账户 DTO
/// </summary>
public record AvailableAccountTokenOutputDto
{
    /// <summary>
    /// 账户 ID
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// 提供商
    /// </summary>
    public required Provider Provider { get; init; }

    /// <summary>
    /// 认证方式
    /// </summary>
    public required AuthMethod AuthMethod { get; init; }

    /// <summary>
    /// 账户名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 额外属性
    /// </summary>
    public Dictionary<string, string> ExtraProperties { get; init; } = new();

    /// <summary>
    /// 上游访问令牌
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// 自定义上游地址
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// 最大并发数
    /// </summary>
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// 当前并发数
    /// </summary>
    public int CurrentConcurrency { get; init; }

    /// <summary>
    /// 模型白名单
    /// </summary>
    public List<string>? ModelWhites { get; init; }

    /// <summary>
    /// 模型映射规则
    /// </summary>
    public Dictionary<string, string>? ModelMapping { get; init; }

    /// <summary>
    /// 限流控制范围
    /// </summary>
    public RateLimitScope RateLimitScope { get; init; }

    /// <summary>
    /// 是否允许伪装为官方客户端
    /// </summary>
    public bool AllowOfficialClientMimic { get; init; }

    /// <summary>
    /// 是否启用流健康检查
    /// </summary>
    public bool IsCheckStreamHealth { get; init; }
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 提供商账户输出 DTO
/// </summary>
public class AccountTokenOutputDto
{
    /// <summary>
    /// ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 账户名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 平台类型
    /// </summary>
    public ProviderPlatform Platform { get; init; }

    /// <summary>
    /// 额外属性
    /// </summary>
    public Dictionary<string, string> ExtraProperties { get; init; } = new();

    /// <summary>
    /// Base URL
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// 描述说明
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// 账户状态
    /// </summary>
    public AccountStatus Status { get; init; }

    /// <summary>
    /// 状态描述 (如限流原因)
    /// </summary>
    public string? StatusDescription { get; init; }

    /// <summary>
    /// 限流时长（秒）
    /// </summary>
    public int? RateLimitDurationSeconds { get; init; }

    /// <summary>
    /// 锁定直到 (UTC)
    /// </summary>
    public DateTime? LockedUntil { get; init; }

    /// <summary>
    /// 最大并发数（0 表示不限制）
    /// </summary>
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// 当前并发数（实时数据，非持久化）
    /// </summary>
    public int CurrentConcurrency { get; set; }

    /// <summary>
    /// 完整 Token (敏感信息，仅编辑时返回)
    /// </summary>
    public string FullToken { get; init; } = string.Empty;

    /// <summary>
    /// Token 获取时间
    /// </summary>
    public DateTime? TokenObtainedTime { get; init; }

    /// <summary>
    /// Token 过期时间（秒）
    /// </summary>
    public long? ExpiresIn { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreationTime { get; init; }

    /// <summary>
    /// 今日使用次数
    /// </summary>
    public long UsageToday { get; set; } // Mutable to allow stats filling after mapping

    /// <summary>
    /// 总使用次数
    /// </summary>
    public long UsageTotal { get; set; } // Mutable

    /// <summary>
    /// 成功率 (0-100)
    /// </summary>
    public decimal SuccessRate { get; set; } // Mutable
}

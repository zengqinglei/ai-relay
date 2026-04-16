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
    /// 提供商
    /// </summary>
    public Provider Provider { get; init; }

    /// <summary>
    /// 认证方式
    /// </summary>
    public AuthMethod AuthMethod { get; init; }

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
    /// 调度优先级（值越小优先级越高）
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// 调度权重（同优先级内按权重分配）
    /// </summary>
    public int Weight { get; init; }

    /// <summary>
    /// 所属分组ID列表
    /// </summary>
    public List<Guid> ProviderGroupIds { get; set; } = [];

    /// <summary>
    /// 该账户支持的路由协议
    /// </summary>
    public List<RouteProfile> SupportedRouteProfiles { get; set; } = [];

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
    /// 今日调用次数
    /// </summary>
    public long UsageToday { get; set; }

    /// <summary>
    /// 累计调用次数
    /// </summary>
    public long UsageTotal { get; set; }

    /// <summary>
    /// 今日消耗额度（USD）
    /// </summary>
    public decimal CostToday { get; set; }

    /// <summary>
    /// 累计消耗额度（USD）
    /// </summary>
    public decimal CostTotal { get; set; }

    /// <summary>
    /// 今日消耗 Token 数
    /// </summary>
    public long TokensToday { get; set; }

    /// <summary>
    /// 累计消耗 Token 数
    /// </summary>
    public long TokensTotal { get; set; }

    /// <summary>
    /// 今日成功率 (0-100)
    /// </summary>
    public decimal SuccessRateToday { get; set; }

    /// <summary>
    /// 累计成功率 (0-100)
    /// </summary>
    public decimal SuccessRateTotal { get; set; }

    /// <summary>
    /// 模型白名单（限制可接受的模型）
    /// </summary>
    public List<string>? ModelWhites { get; set; }

    /// <summary>
    /// 模型映射规则（转换模型名称）
    /// </summary>
    public Dictionary<string, string>? ModelMapping { get; set; }

    /// <summary>
    /// 是否允许伪装为官方客户端
    /// </summary>
    public bool AllowOfficialClientMimic { get; init; }

    /// <summary>
    /// 流健康度检查 (主动拦截没有输出或是带有错误抛出的残缺数据流)
    /// </summary>
    public bool IsCheckStreamHealth { get; init; }
}

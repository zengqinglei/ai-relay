using AiRelay.Application.ProviderAccounts.Dtos;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 选择账号结果（包含分组信息）
/// </summary>
public record SelectAccountResultDto
{
    /// <summary>
    /// 选中的账号
    /// </summary>
    public required AvailableAccountTokenOutputDto AccountToken { get; init; }

    /// <summary>
    /// 提供商分组 ID（用于计费快照）
    /// </summary>
    public Guid ProviderGroupId { get; init; }

    /// <summary>
    /// 提供商分组名称（用于冗余字段）
    /// </summary>
    public required string ProviderGroupName { get; init; }

    /// <summary>
    /// 分组费率倍数（用于计费快照）
    /// </summary>
    public decimal GroupRateMultiplier { get; init; }

    /// <summary>
    /// 等待计划（决定是否需要等待当前账号）
    /// </summary>
    public WaitPlan WaitPlan { get; init; } = WaitPlan.Default;

    /// <summary>
    /// 可用账号总数（用于判断是否还有其他账号可切换）
    /// </summary>
    public int AvailableAccountCount { get; init; }

    /// <summary>
    /// 账号当前退避计数（用于动态调整同账号重试次数）
    /// </summary>
    public int BackoffCount { get; init; }
}

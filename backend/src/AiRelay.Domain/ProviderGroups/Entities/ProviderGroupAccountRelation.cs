using AiRelay.Domain.ProviderAccounts.Entities;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ProviderGroups.Entities;

/// <summary>
/// 分组与账户的关联关系
/// </summary>
public class ProviderGroupAccountRelation : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// 分组ID
    /// </summary>
    public Guid ProviderGroupId { get; private set; }

    /// <summary>
    /// 账户TokenID
    /// </summary>
    public Guid AccountTokenId { get; private set; }

    /// <summary>
    /// 优先级（用于 Priority 策略，值越小优先级越高）
    /// </summary>
    public int Priority { get; private set; }

    /// <summary>
    /// 权重（用于 WeightedRandom 策略，1-1000）
    /// </summary>
    public int Weight { get; private set; } = 1;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; private set; } = true;

    // 导航属性
    public ProviderGroup ProviderGroup { get; private set; } = null!;
    public AccountToken? AccountToken { get; private set; }

    // EF Core 私有构造函数
    private ProviderGroupAccountRelation() { }

    /// <summary>
    /// 创建关联
    /// </summary>
    public ProviderGroupAccountRelation(
        Guid providerGroupId,
        Guid accountTokenId,
        int priority = 0,
        int weight = 1)
    {
        Id = Guid.CreateVersion7();
        ProviderGroupId = providerGroupId;
        AccountTokenId = accountTokenId;
        Priority = priority;
        Weight = weight;
    }

    /// <summary>
    /// 更新优先级
    /// </summary>
    public void UpdatePriority(int priority) => Priority = priority;

    /// <summary>
    /// 更新权重
    /// </summary>
    public void UpdateWeight(int weight)
    {
        Weight = weight;
    }

    /// <summary>
    /// 启用
    /// </summary>
    public void Enable() => IsActive = true;

    /// <summary>
    /// 禁用
    /// </summary>
    public void Disable() => IsActive = false;
}

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
    public ProviderGroupAccountRelation(Guid providerGroupId, Guid accountTokenId)
    {
        Id = Guid.CreateVersion7();
        ProviderGroupId = providerGroupId;
        AccountTokenId = accountTokenId;
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

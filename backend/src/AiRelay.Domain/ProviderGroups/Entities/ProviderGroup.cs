using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderGroups.ValueObjects;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ProviderGroups.Entities;

/// <summary>
/// 提供商账户分组
/// </summary>
public class ProviderGroup : FullAuditedEntity<Guid>
{
    public string Name { get; private set; }

    public string? Description { get; private set; }



    public GroupSchedulingStrategy SchedulingStrategy { get; private set; }

    public bool EnableStickySession { get; private set; }

    public int StickySessionExpirationHours { get; private set; } = 1;

    public decimal RateMultiplier { get; private set; } = 1.0m;
    
    // 导航属性
    public virtual ICollection<ProviderGroupAccountRelation> Relations { get; private set; } = new List<ProviderGroupAccountRelation>();

    /// <summary>
    /// ApiKey 绑定关系 (反向导航)
    /// </summary>
    public virtual ICollection<ApiKeyProviderGroupBinding> ApiKeyBindings { get; private set; } = new List<ApiKeyProviderGroupBinding>();

    private ProviderGroup() => Name = null!;

    public ProviderGroup(
        string name,
        string? description,
        GroupSchedulingStrategy schedulingStrategy,
        bool enableStickySession = false,
        int stickySessionExpirationHours = 1,
        decimal rateMultiplier = 1.0m)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Description = description;

        SchedulingStrategy = schedulingStrategy;
        EnableStickySession = enableStickySession;
        StickySessionExpirationHours = stickySessionExpirationHours;
        RateMultiplier = rateMultiplier;
    }

    public void Update(string name, string? description, GroupSchedulingStrategy schedulingStrategy, decimal rateMultiplier)
    {
        Name = name;
        Description = description;
        SchedulingStrategy = schedulingStrategy;
        RateMultiplier = rateMultiplier;
    }

    public void UpdateStickySession(bool enable, int expirationHours = 1)
    {
        EnableStickySession = enable;
        if (enable)
        {
            StickySessionExpirationHours = expirationHours;
        }
    }
}

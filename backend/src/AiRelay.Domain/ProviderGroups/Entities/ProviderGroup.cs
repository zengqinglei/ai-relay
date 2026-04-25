using AiRelay.Domain.ApiKeys.Entities;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ProviderGroups.Entities;

/// <summary>
/// 提供商账户分组
/// </summary>
public class ProviderGroup : FullAuditedEntity<Guid>
{
    public string Name { get; private set; }

    public string? Description { get; private set; }

    public bool IsDefault { get; private set; }

    public bool EnableStickySession { get; private set; }

    public int StickySessionExpirationHours { get; private set; } = 1;

    public decimal RateMultiplier { get; private set; } = 1.0m;

    // 导航属性
    public virtual ICollection<ProviderGroupAccountRelation> Relations { get; private set; } = new List<ProviderGroupAccountRelation>();
    public virtual ICollection<ProviderGroupAssignedUser> AssignedUsers { get; private set; } = new List<ProviderGroupAssignedUser>();

    /// <summary>
    /// ApiKey 绑定关系 (反向导航)
    /// </summary>
    public virtual ICollection<ApiKeyProviderGroupBinding> ApiKeyBindings { get; private set; } = new List<ApiKeyProviderGroupBinding>();

    private ProviderGroup() => Name = null!;

    public ProviderGroup(
        string name,
        string? description,
        IEnumerable<Guid>? assignedUserIds = null,
        bool isDefault = false,
        bool enableStickySession = false,
        int stickySessionExpirationHours = 1,
        decimal rateMultiplier = 1.0m)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Description = description;
        IsDefault = isDefault;
        EnableStickySession = enableStickySession;
        StickySessionExpirationHours = stickySessionExpirationHours;
        RateMultiplier = rateMultiplier;
        ReplaceAssignedUsers(assignedUserIds);
    }

    public void Update(string name, string? description, IEnumerable<Guid>? assignedUserIds, decimal rateMultiplier)
    {
        Name = name;
        Description = description;
        RateMultiplier = rateMultiplier;
        ReplaceAssignedUsers(assignedUserIds);
    }

    public void UpdateStickySession(bool enable, int expirationHours = 1)
    {
        EnableStickySession = enable;
        if (enable)
        {
            StickySessionExpirationHours = expirationHours;
        }
    }

    public void MarkAsDefault(string name = "default")
    {
        IsDefault = true;
        Name = name;
        AssignedUsers.Clear();
    }

    public bool IsAssignedTo(Guid userId)
    {
        return AssignedUsers.Any(x => x.UserId == userId);
    }

    public bool IsPublic()
    {
        return AssignedUsers.Count == 0;
    }

    private void ReplaceAssignedUsers(IEnumerable<Guid>? assignedUserIds)
    {
        AssignedUsers.Clear();

        foreach (var userId in assignedUserIds?
                     .Where(x => x != Guid.Empty)
                     .Distinct()
                 ?? [])
        {
            AssignedUsers.Add(new ProviderGroupAssignedUser(Id, userId));
        }
    }
}

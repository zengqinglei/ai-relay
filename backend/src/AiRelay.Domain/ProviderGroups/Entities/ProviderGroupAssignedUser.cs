using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ProviderGroups.Entities;

/// <summary>
/// 分组分配用户
/// </summary>
public class ProviderGroupAssignedUser : DeletionAuditedEntity<Guid>
{
    public Guid ProviderGroupId { get; private set; }

    public Guid UserId { get; private set; }

    public virtual ProviderGroup ProviderGroup { get; private set; } = null!;

    private ProviderGroupAssignedUser()
    {
    }

    public ProviderGroupAssignedUser(Guid providerGroupId, Guid userId)
    {
        Id = Guid.CreateVersion7();
        ProviderGroupId = providerGroupId;
        UserId = userId;
    }
}

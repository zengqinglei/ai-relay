using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.Users.Entities;

/// <summary>
/// 用户角色关联实体
/// </summary>
public class UserRole : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// 角色 ID
    /// </summary>
    public Guid RoleId { get; private set; }

    /// <summary>
    /// 导航属性 - 用户
    /// </summary>
    public User? User { get; private set; }

    /// <summary>
    /// 导航属性 - 角色
    /// </summary>
    public Role? Role { get; private set; }

    private UserRole() { }

    public UserRole(Guid userId, Guid roleId)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        RoleId = roleId;
    }
}

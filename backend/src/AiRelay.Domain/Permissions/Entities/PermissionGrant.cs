using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.Permissions.Entities;

/// <summary>
/// 权限授予实体（角色 -> 权限）
/// </summary>
public class PermissionGrant : CreationAuditedEntity<Guid>
{
    /// <summary>
    /// 权限名称（如：ApiProxy.Access）
    /// </summary>
    public string PermissionName { get; private set; }

    /// <summary>
    /// 授予对象类型（Role, User）
    /// </summary>
    public string ProviderName { get; private set; }

    /// <summary>
    /// 授予对象的 Key（RoleId 或 UserId）
    /// </summary>
    public string ProviderKey { get; private set; }

    private PermissionGrant()
    {
        PermissionName = null!;
        ProviderName = null!;
        ProviderKey = null!;
    }

    public PermissionGrant(
        string permissionName,
        string providerName,
        string providerKey)
    {
        Id = Guid.CreateVersion7();
        PermissionName = permissionName ?? throw new ArgumentNullException(nameof(permissionName));
        ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
        ProviderKey = providerKey ?? throw new ArgumentNullException(nameof(providerKey));
    }

    public static PermissionGrant ForRole(string permissionName, Guid roleId)
    {
        return new PermissionGrant(permissionName, "Role", roleId.ToString());
    }

    public static PermissionGrant ForUser(string permissionName, Guid userId)
    {
        return new PermissionGrant(permissionName, "User", userId.ToString());
    }
}

using System.Security.Claims;
using AiRelay.Domain.Permissions.Entities;
using AiRelay.Domain.Permissions.Specifications;
using AiRelay.Domain.Users.Constants;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Application.Permission;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Security.Users;

namespace AiRelay.Application.Permissions.Checker;

/// <summary>
/// 权限检查器实现
/// </summary>
public class PermissionChecker(
    ICurrentUser currentUser,
    IRepository<PermissionGrant, Guid> permissionGrantRepository,
    IRepository<UserRole, Guid> userRoleRepository) : IPermissionChecker
{
    public Task<bool> IsGrantedAsync(string name, CancellationToken cancellationToken = default)
    {
        return IsGrantedAsync(null, name, cancellationToken);
    }

    public async Task<bool> IsGrantedAsync(
        ClaimsPrincipal? claimsPrincipal,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        Guid? userId;
        string[] roles;

        if (claimsPrincipal != null)
        {
            var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier);
            userId = userIdClaim != null ? Guid.Parse(userIdClaim.Value) : null;
            roles = claimsPrincipal.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray();
        }
        else
        {
            userId = currentUser.Id;
            roles = currentUser.GetRoles();
        }

        if (!userId.HasValue)
            return false;

        if (roles.Contains(AdminConstant.RoleName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var userIdValue = userId.Value;

        var userGrant = await permissionGrantRepository.GetFirstAsync(
            PermissionGrantSpecifications.ByPermissionAndProvider(name, "User", userIdValue.ToString()),
            cancellationToken: cancellationToken
        );

        if (userGrant != null)
            return true;

        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == userIdValue, cancellationToken);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();

        foreach (var roleId in roleIds)
        {
            var roleGrant = await permissionGrantRepository.GetFirstAsync(
                PermissionGrantSpecifications.ByPermissionAndProvider(name, "Role", roleId.ToString()),
                cancellationToken: cancellationToken
            );

            if (roleGrant != null)
                return true;
        }

        return false;
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(
        string[] names,
        CancellationToken cancellationToken = default)
    {
        return IsGrantedAsync(null, names, cancellationToken);
    }

    public async Task<MultiplePermissionGrantResult> IsGrantedAsync(
        ClaimsPrincipal? claimsPrincipal,
        string[] names,
        CancellationToken cancellationToken = default)
    {
        if (names == null || names.Length == 0)
            return new MultiplePermissionGrantResult(new Dictionary<string, bool>());

        var results = new Dictionary<string, bool>();

        foreach (var name in names)
        {
            results[name] = await IsGrantedAsync(claimsPrincipal, name, cancellationToken);
        }

        return new MultiplePermissionGrantResult(results);
    }
}

using System.Security.Claims;
using Leistd.Security.Claims;

namespace Leistd.Security.Users;

/// <summary>
/// 当前用户实现
/// </summary>
/// <param name="principalAccessor">认证主体访问器</param>
public class CurrentUser(ICurrentPrincipalAccessor principalAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => principalAccessor.Principal;

    /// <inheritdoc />
    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public Guid? Id
    {
        get
        {
            var idValue = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idValue, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public string? Username =>
        Principal?.FindFirst(ClaimTypes.Name)?.Value;

    /// <inheritdoc />
    public string? Name =>
        Principal?.FindFirst(ClaimTypes.GivenName)?.Value;

    /// <inheritdoc />
    public string? Email =>
        Principal?.FindFirst(ClaimTypes.Email)?.Value;

    /// <inheritdoc />
    public string? PhoneNumber =>
        Principal?.FindFirst(ClaimTypes.MobilePhone)?.Value;

    /// <inheritdoc />
    public string[] GetRoles() =>
        Principal?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray() ?? [];

    /// <inheritdoc />
    public bool IsInRole(string roleName) =>
        GetRoles().Contains(roleName, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Claim? FindClaim(string claimType) =>
        Principal?.FindFirst(claimType);

    /// <inheritdoc />
    public Claim[] FindClaims(string claimType) =>
        Principal?.FindAll(claimType).ToArray() ?? [];

    /// <inheritdoc />
    public Claim[] GetAllClaims() =>
        Principal?.Claims.ToArray() ?? [];
}

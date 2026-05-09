using System.Security.Claims;
using Leistd.Security.Claims;

namespace Leistd.Security.Users;

/// <summary>
/// 当前用户实现
/// </summary>
/// <param name="principalAccessor">认证主体访问器</param>
public class CurrentUser(ICurrentPrincipalAccessor principalAccessor) : ICurrentUser
{
    private const string SubjectClaimType = "sub";
    private const string NameClaimType = "name";
    private const string PreferredUsernameClaimType = "preferred_username";
    private const string EmailClaimType = "email";
    private const string RoleClaimType = "role";

    private ClaimsPrincipal? Principal => principalAccessor.Principal;

    /// <inheritdoc />
    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public Guid? Id
    {
        get
        {
            var idValue = FindFirstValue(SubjectClaimType, ClaimTypes.NameIdentifier);
            return Guid.TryParse(idValue, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public string? Username =>
        FindFirstValue(PreferredUsernameClaimType, NameClaimType, ClaimTypes.Name);

    /// <inheritdoc />
    public string? Name =>
        FindFirstValue(NameClaimType, ClaimTypes.GivenName);

    /// <inheritdoc />
    public string? Email =>
        FindFirstValue(EmailClaimType, ClaimTypes.Email);

    /// <inheritdoc />
    public string? PhoneNumber =>
        Principal?.FindFirst(ClaimTypes.MobilePhone)?.Value;

    /// <inheritdoc />
    public string[] GetRoles() =>
        Principal == null
            ? []
            : Principal.FindAll(RoleClaimType)
                .Concat(Principal.FindAll(ClaimTypes.Role))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

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

    private string? FindFirstValue(params string[] claimTypes)
    {
        if (Principal == null)
            return null;

        foreach (var claimType in claimTypes)
        {
            var value = Principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}

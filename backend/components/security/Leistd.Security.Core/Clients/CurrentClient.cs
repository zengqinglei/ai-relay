using System.Security.Claims;
using Leistd.Security.Claims;

namespace Leistd.Security.Clients;

/// <summary>
/// 当前客户端实现
/// </summary>
/// <param name="principalAccessor">认证主体访问器</param>
public class CurrentClient(ICurrentPrincipalAccessor principalAccessor) : ICurrentClient
{
    private ClaimsPrincipal? Principal => principalAccessor.Principal;

    /// <inheritdoc />
    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(ClientId) || ApiKeyId.HasValue;

    /// <inheritdoc />
    public string? ClientId =>
        Principal?.FindFirst(CustomClaimTypes.ClientId)?.Value;

    /// <inheritdoc />
    public Guid? ApiKeyId
    {
        get
        {
            var idValue = Principal?.FindFirst("api_key_id")?.Value;
            return Guid.TryParse(idValue, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public Guid? CreatorId
    {
        get
        {
            var idValue = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idValue, out var id) ? id : null;
        }
    }
}

using System.Security.Claims;
using Leistd.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Leistd.Security.AspNetCore.Claims;

/// <summary>
/// 基于 HttpContext 的认证主体访问器
/// </summary>
/// <param name="httpContextAccessor">HTTP 上下文访问器</param>
public class HttpContextCurrentPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    : CurrentPrincipalAccessor
{
    /// <inheritdoc />
    protected override ClaimsPrincipal? GetClaimsPrincipal()
    {
        return httpContextAccessor.HttpContext?.User;
    }
}

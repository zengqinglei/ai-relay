using System.Security.Claims;
using AiRelay.Application.Auth.AppServices;
using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 外部认证控制器
/// </summary>
[Route("api/v1/external-auth")]
public class ExternalAuthController(
    IExternalAuthAppService externalAuthAppService,
    UserDomainService userDomainService) : BaseController
{
    /// <summary>
    /// 获取外部登录 URL（GitHub, Google）
    /// </summary>
    /// <param name="provider">提供商名称（github, google）</param>
    [AllowAnonymous]
    [HttpGet("{provider}/login-url")]
    public ExternalLoginUrlOutputDto GetLoginUrl(string provider)
    {
        return externalAuthAppService.GetLoginUrl(provider);
    }

    /// <summary>
    /// 处理外部登录回调
    /// </summary>
    /// <param name="provider">提供商名称（github, google）</param>
    /// <param name="request">回调请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    [AllowAnonymous]
    [HttpPost("{provider}/callback")]
    public async Task<IActionResult> CallbackAsync(
        string provider,
        [FromBody] ExternalLoginCallbackInputDto request,
        CancellationToken cancellationToken)
    {
        var user = await externalAuthAppService.AuthenticateExternalUserAsync(provider, request, cancellationToken);

        var principal = await CreateCookiePrincipalAsync(user, cancellationToken);

        await HttpContext.SignInAsync("AiRelayCookie", principal,
            new AuthenticationProperties { IsPersistent = true });
        return NoContent();
    }

    private async Task<ClaimsPrincipal> CreateCookiePrincipalAsync(User user, CancellationToken cancellationToken)
    {
        var identity = new ClaimsIdentity("AiRelayCookie");
        identity.AddClaim(new Claim(Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));

        foreach (var roleName in await userDomainService.GetUserRoleNamesAsync(user.Id, cancellationToken))
        {
            identity.AddClaim(new Claim("role", roleName));
        }

        return new ClaimsPrincipal(identity);
    }
}

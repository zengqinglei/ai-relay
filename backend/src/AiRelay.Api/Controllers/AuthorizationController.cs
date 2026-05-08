using AiRelay.Application.Auth.AppServices;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AiRelay.Api.Controllers;

public class AuthorizationController(
    IRepository<User, Guid> userRepository,
    IAuthPrincipalFactory principalFactory) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AuthorizeAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenID Connect authorization request is unavailable.");

        var result = await HttpContext.AuthenticateAsync("AiRelayCookie");
        if (!result.Succeeded || result.Principal == null)
        {
            var returnUrl = Request.PathBase + Request.Path + QueryString.Create(
                Request.HasFormContentType
                    ? Request.Form.Select(parameter => new KeyValuePair<string, string?>(parameter.Key, parameter.Value))
                    : Request.Query.Select(parameter => new KeyValuePair<string, string?>(parameter.Key, parameter.Value)));

            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var subject = result.Principal.GetClaim(Claims.Subject) ??
                      result.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive || user.IsLockedOut())
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var principal = await principalFactory.CreateAsync(user, request.GetScopes(), cancellationToken);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LogoutAsync()
    {
        await HttpContext.SignOutAsync("AiRelayCookie");
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> ExchangeAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenID Connect token request is unavailable.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var subject = result.Principal?.GetClaim(Claims.Subject);
            if (!result.Succeeded || !Guid.TryParse(subject, out var userId))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null || !user.IsActive || user.IsLockedOut())
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var scopes = request.GetScopes().Any()
                ? request.GetScopes()
                : result.Principal?.GetScopes() ?? [];
            var principal = await principalFactory.CreateAsync(user, scopes, cancellationToken);
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new BadRequestException($"不支持的授权类型: {request.GrantType}");
    }

    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Produces("application/json")]
    public async Task<IActionResult> UserInfoAsync(CancellationToken cancellationToken)
    {
        var subject = User.GetClaim(Claims.Subject);
        if (!Guid.TryParse(subject, out var userId))
        {
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.Id.ToString()
        };

        if (User.HasScope(Scopes.Profile))
        {
            claims[Claims.Name] = user.Nickname ?? user.Username;
            claims[Claims.PreferredUsername] = user.Username;
            if (Uri.TryCreate(user.Avatar, UriKind.Absolute, out var avatarUri) &&
                (avatarUri.Scheme == Uri.UriSchemeHttp || avatarUri.Scheme == Uri.UriSchemeHttps))
            {
                claims[Claims.Picture] = user.Avatar;
            }
        }

        if (User.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = user.Email;
            claims[Claims.EmailVerified] = user.EmailConfirmed;
        }

        if (User.HasScope(Scopes.Roles))
        {
            claims[Claims.Role] = User.GetClaims(Claims.Role).ToArray();
        }

        return Ok(claims);
    }
}

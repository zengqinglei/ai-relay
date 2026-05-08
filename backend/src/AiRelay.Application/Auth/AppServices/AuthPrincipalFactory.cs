using System.Security.Claims;
using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AiRelay.Application.Auth.AppServices;

public class AuthPrincipalFactory(UserDomainService userDomainService) : IAuthPrincipalFactory
{
    public async Task<ClaimsPrincipal> CreateAsync(
        User user,
        IEnumerable<string>? scopes = null,
        CancellationToken cancellationToken = default)
    {
        var roleNames = await userDomainService.GetUserRoleNamesAsync(user.Id, cancellationToken);
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

        identity.AddClaim(Claims.Subject, user.Id.ToString());
        identity.AddClaim(ClaimTypes.NameIdentifier, user.Id.ToString());
        identity.AddClaim(Claims.Name, user.Username);
        identity.AddClaim(ClaimTypes.Name, user.Username);
        identity.AddClaim(Claims.PreferredUsername, user.Username);
        identity.AddClaim(Claims.Email, user.Email);
        identity.AddClaim(ClaimTypes.Email, user.Email);

        if (!string.IsNullOrWhiteSpace(user.Nickname))
        {
            identity.AddClaim(Claims.GivenName, user.Nickname);
            identity.AddClaim(ClaimTypes.GivenName, user.Nickname);
        }

        if (IsHttpUrl(user.Avatar))
        {
            identity.AddClaim(Claims.Picture, user.Avatar!);
        }

        foreach (var role in roleNames)
        {
            identity.AddClaim(Claims.Role, role);
            identity.AddClaim(ClaimTypes.Role, role);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes?.Where(scope => !string.IsNullOrWhiteSpace(scope)) ??
                            [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles]);
        principal.SetResources("ai-relay-api");
        principal.SetDestinations(GetDestinations);

        return principal;
    }

    private static bool IsHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            Claims.Subject =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],
            ClaimTypes.NameIdentifier => [Destinations.AccessToken],
            Claims.Name or Claims.PreferredUsername or Claims.GivenName
                when claim.Subject?.HasScope(Scopes.Profile) == true =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],
            ClaimTypes.Name or ClaimTypes.GivenName
                when claim.Subject?.HasScope(Scopes.Profile) == true => [Destinations.AccessToken],
            Claims.Email when claim.Subject?.HasScope(Scopes.Email) == true =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],
            ClaimTypes.Email when claim.Subject?.HasScope(Scopes.Email) == true => [Destinations.AccessToken],
            Claims.Role when claim.Subject?.HasScope(Scopes.Roles) == true =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],
            ClaimTypes.Role when claim.Subject?.HasScope(Scopes.Roles) == true => [Destinations.AccessToken],
            _ => []
        };
    }
}

using System.Collections.Immutable;
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

        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(Claims.Name, user.Nickname ?? user.Username);
        identity.SetClaim(Claims.PreferredUsername, user.Username);
        identity.SetClaim(Claims.Email, user.Email);

        if (IsHttpUrl(user.Avatar))
        {
            identity.SetClaim(Claims.Picture, user.Avatar!);
        }

        identity.SetClaims(Claims.Role, roleNames.ToImmutableArray());

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
            Claims.Name or Claims.PreferredUsername or Claims.Picture
                when claim.Subject?.HasScope(Scopes.Profile) == true =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],
            Claims.Email when claim.Subject?.HasScope(Scopes.Email) == true =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],
            Claims.Role when claim.Subject?.HasScope(Scopes.Roles) == true =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],
            _ => []
        };
    }
}

using System.Security.Claims;
using AiRelay.Domain.Users.Entities;

namespace AiRelay.Application.Auth.AppServices;

public interface IAuthPrincipalFactory
{
    Task<ClaimsPrincipal> CreateAsync(User user, IEnumerable<string>? scopes = null, CancellationToken cancellationToken = default);
}

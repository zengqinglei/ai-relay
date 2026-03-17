using AiRelay.Application.ApiKeys.AppServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace AiRelay.Api.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiKeyAppService _apiKeyAppService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyAppService apiKeyAppService)
        : base(options, logger, encoder)
    {
        _apiKeyAppService = apiKeyAppService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var endpoint = Context.GetEndpoint();

        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            return AuthenticateResult.NoResult();
        }

        var authorizeData = endpoint?.Metadata?.GetMetadata<IAuthorizeData>();

        if (authorizeData == null)
        {
            return AuthenticateResult.NoResult();
        }

        if (!string.IsNullOrEmpty(authorizeData.Policy) &&
            !authorizeData.Policy.Equals(AuthorizationPolicies.AiProxyPolicy, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = Request.GetAiRelayApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var validationResult = await _apiKeyAppService.ValidateAsync(apiKey);
        if (!validationResult.IsValid)
        {
            var maskedKey = apiKey.Length > 8 ? apiKey.Substring(0, 8) + "***" : "***";
            Logger.LogWarning("验证 ApiKey 失败：{Key}，原因：{Reason}",
                maskedKey, validationResult.FailureReason);
            return AuthenticateResult.Fail(validationResult.FailureReason ?? "Invalid ApiKey");
        }

        var claims = new[]
        {
            new Claim(AuthenticationConstants.ApiKeyIdClaimType, validationResult.ApiKeyId!.Value.ToString()),
            new Claim(AuthenticationConstants.ApiKeyNameClaimType, validationResult.Name!.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

}

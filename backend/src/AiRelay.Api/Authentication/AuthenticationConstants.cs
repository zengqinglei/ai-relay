namespace AiRelay.Api.Authentication;

public static class AuthenticationConstants
{
    public const string ApiKeyHeaderName = "x-api-key";
    public const string GoogApiKeyHeaderName = "x-goog-api-key";
    public const string AuthorizationHeaderName = "Authorization";
    public const string ApiKeyQueryParameter = "api_key";
    public const string BearerPrefix = "Bearer ";

    // Claims
    public const string ApiKeyIdClaimType = "api_key_id";
    public const string ApiKeyNameClaimType = "api_key_name";
}

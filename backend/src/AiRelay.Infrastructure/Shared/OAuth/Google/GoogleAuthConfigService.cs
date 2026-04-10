using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.OAuth.Google;
using Leistd.Exception.Core;

namespace AiRelay.Infrastructure.Shared.OAuth.Google;

/// <summary>
/// Google OAuth 配置服务实现
/// </summary>
public class GoogleAuthConfigService : IGoogleAuthConfigService
{
    public GoogleAuthConfig GetConfig(Provider provider)
    {
        return provider switch
        {
            Provider.Antigravity => new GoogleAuthConfig
            {
                ClientId = "1071006060591-tmhssin2h21lcre235vtolojh4g403ep.apps.googleusercontent.com",
                ClientSecret = "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",
                RedirectUri = "http://localhost:8085/callback",
                Scopes = "https://www.googleapis.com/auth/cloud-platform " +
                         "https://www.googleapis.com/auth/userinfo.email " +
                         "https://www.googleapis.com/auth/userinfo.profile " +
                         "https://www.googleapis.com/auth/cclog " +
                         "https://www.googleapis.com/auth/experimentsandconfigs"
            },
            Provider.Gemini => new GoogleAuthConfig
            {
                ClientId = "681255809395-oo8ft2oprdrnp9e3aqf6av3hmdib135j.apps.googleusercontent.com",
                ClientSecret = "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",
                RedirectUri = "https://codeassist.google.com/authcode",
                Scopes = "https://www.googleapis.com/auth/cloud-platform " +
                         "https://www.googleapis.com/auth/userinfo.email " +
                         "https://www.googleapis.com/auth/userinfo.profile"
            },
            _ => throw new BadRequestException($"提供商 {provider} 不支持 Google OAuth 配置")
        };
    }
}

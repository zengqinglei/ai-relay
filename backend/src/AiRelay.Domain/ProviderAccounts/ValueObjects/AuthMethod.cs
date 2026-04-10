namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 认证方式（决定凭证类型、认证头格式）
/// </summary>
public enum AuthMethod
{
    OAuth,
    ApiKey
}

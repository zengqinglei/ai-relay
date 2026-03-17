namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 账户状态枚举
/// </summary>
public enum AccountStatus
{
    Normal = 0,
    RateLimited = 1,
    Error = 2
}

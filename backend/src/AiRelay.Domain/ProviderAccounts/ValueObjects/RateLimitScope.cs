namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 限流控制范围
/// </summary>
public enum RateLimitScope
{
    /// <summary>
    /// 任一限流作用于整个账户
    /// </summary>
    Account = 0,

    /// <summary>
    /// 仅锁定当前触发限流的模型
    /// </summary>
    Model = 1
}

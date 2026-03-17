namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 使用记录状态
/// </summary>
public enum UsageStatus
{
    /// <summary>
    /// 进行中（已开始，未完成）
    /// </summary>
    InProgress = 0,

    /// <summary>
    /// 成功
    /// </summary>
    Success = 1,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 2
}

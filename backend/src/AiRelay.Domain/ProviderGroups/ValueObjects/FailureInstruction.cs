namespace AiRelay.Domain.ProviderGroups.ValueObjects;

/// <summary>
/// 失败处理指令
/// </summary>
public enum FailureInstruction
{
    /// <summary>
    /// 直接失败（向上抛出异常）
    /// </summary>
    Fail,

    /// <summary>
    /// 在同一账号上重试
    /// </summary>
    RetrySameAccount,

    /// <summary>
    /// 切换账号重试
    /// </summary>
    SwitchAccount
}

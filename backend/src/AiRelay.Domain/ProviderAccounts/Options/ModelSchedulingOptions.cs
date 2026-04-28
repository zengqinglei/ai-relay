namespace AiRelay.Domain.ProviderAccounts.Options;

/// <summary>
/// 模型调度策略参数。
/// 当前仅提供代码内默认值，暂不开放 appsettings 配置。
/// </summary>
public class ModelSchedulingOptions
{
    /// <summary>
    /// 单次请求最多允许切换的账号数量。
    /// </summary>
    public int MaxAccountSwitches { get; set; } = 5;

    /// <summary>
    /// 粘性会话账号的最长等待时间（秒）。
    /// </summary>
    public int StickyWaitTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 非粘性账号的兜底等待时间（秒）。
    /// </summary>
    public int NonStickyWaitTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 等待队列长度相对最大并发数的缓冲值。
    /// </summary>
    public int WaitQueueBufferSize { get; set; } = 20;

    /// <summary>
    /// 当上游显式要求等待超过该阈值时，优先切号而不是继续同号等待（秒）。
    /// </summary>
    public int LongRetryAfterSwitchThresholdSeconds { get; set; } = 30;
}

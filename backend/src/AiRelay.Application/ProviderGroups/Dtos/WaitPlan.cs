namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 等待计划
/// </summary>
public class WaitPlan
{
    /// <summary>
    /// 默认实例（无需等待）
    /// </summary>
    public static readonly WaitPlan Default = new();

    /// <summary>
    /// 是否需要等待（粘性会话绑定的账号需要等待）
    /// </summary>
    public bool ShouldWait { get; set; }

    /// <summary>
    /// 等待超时时间
    /// </summary>
    public TimeSpan Timeout { get; set; }

    /// <summary>
    /// 最大并发数
    /// </summary>
    public int MaxConcurrency { get; set; }

    /// <summary>
    /// 是否已绑定粘性会话
    /// </summary>
    public bool IsStickyBound { get; set; }
}

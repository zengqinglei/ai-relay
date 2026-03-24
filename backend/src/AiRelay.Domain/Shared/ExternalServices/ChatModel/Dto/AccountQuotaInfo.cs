namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

/// <summary>
/// 账户配额信息
/// </summary>
public record AccountQuotaInfo
{
    /// <summary>
    /// 模型
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// 剩余配额（如果支持）
    /// </summary>
    public int? RemainingQuota { get; init; }

    /// <summary>
    /// 配额重置时间（如果支持）
    /// </summary>
    public string? QuotaResetTime { get; init; }

    /// <summary>
    /// 订阅层级（如果支持）
    /// </summary>
    public string? SubscriptionTier { get; init; }

    /// <summary>
    /// 最后刷新时间
    /// </summary>
    public DateTime? LastRefreshed { get; init; }
}

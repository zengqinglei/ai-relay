namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

public enum RetryType
{
    /// <summary>不可重试，交由外层决策（切号或最终失败）</summary>
    NoRetry,

    /// <summary>同号重试（可带退避延迟）</summary>
    RetrySameAccount,

    /// <summary>同号降级重试（去掉 thinking 等高级特性后重试）</summary>
    RetrySameAccountWithDowngrade,

    /// <summary>端点在当前账号下不被支持，直接透传响应，不触发熔断</summary>
    UnsupportedEndpoint,
}

public class ModelErrorAnalysisResult
{
    /// <summary>
    /// 重试类型决策
    /// </summary>
    public RetryType RetryType { get; init; } = RetryType.NoRetry;

    /// <summary>
    /// 重试等待时间（仅对 RetrySameAccount / RetrySameAccountWithDowngrade 有意义）
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// 诊断说明（由 Handler 填写，用于日志记录）
    /// </summary>
    public string? Description { get; init; }
}

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

public class ModelErrorAnalysisResult
{
    /// <summary>
    /// 是否允许重试（同账号 or 换号由 Middleware 根据 RetryAfter 决定）
    /// </summary>
    public bool IsCanRetry { get; set; }

    /// <summary>
    /// 重试等待时间（null = 立即/未知）
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }

    /// <summary>
    /// 是否需要降级重试（签名错误场景）
    /// </summary>
    public bool RequiresDowngrade { get; set; }
}

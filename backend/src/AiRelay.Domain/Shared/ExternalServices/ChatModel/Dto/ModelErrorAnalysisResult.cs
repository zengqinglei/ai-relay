namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

public class ModelErrorAnalysisResult
{
    public ModelErrorType ErrorType { get; set; } = ModelErrorType.Unknown;

    public TimeSpan? RetryAfter { get; set; }

    public bool IsRetryableOnSameAccount { get; set; }

    public bool RequiresDowngrade { get; set; }
}

using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.TokenCalculate;

/// <summary>
/// Token 使用量累加器
/// 策略：使用 Max 策略（适用于 OpenAI/Claude/Gemini 的累积式返回）
/// </summary>
public class TokenUsageAccumulator
{
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int CacheReadTokens { get; private set; }
    public int CacheCreationTokens { get; private set; }
    public string? ModelId { get; private set; }

    /// <summary>
    /// 累加 Usage（使用 Max 策略，因为各平台返回的是累积值）
    /// </summary>
    public void Add(ResponseUsage? usage)
    {
        if (usage == null) return;

        if (usage.InputTokens > InputTokens) InputTokens = usage.InputTokens;
        if (usage.OutputTokens > OutputTokens) OutputTokens = usage.OutputTokens;
        if (usage.CacheReadTokens > CacheReadTokens) CacheReadTokens = usage.CacheReadTokens;
        if (usage.CacheCreationTokens > CacheCreationTokens) CacheCreationTokens = usage.CacheCreationTokens;
    }

    public void SetModelId(string? modelId)
    {
        if (!string.IsNullOrEmpty(modelId)) ModelId = modelId;
    }
}

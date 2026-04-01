using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Response;

/// <summary>
/// Usage 累加 Processor — 累加各 ParseSse Processor 解析出的 Usage
/// 使用 Max 策略（各平台返回的是累积值），在完成事件上附带最终的 Usage 和 ModelId
/// </summary>
public class UsageAccumulatorResponseProcessor : IResponseProcessor
{
    private int _inputTokens;
    private int _outputTokens;
    private int _cacheReadTokens;
    private int _cacheCreationTokens;
    private string? _modelId;

    public bool RequiresMutation => false;

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (evt.Type == StreamEventType.Error)
            return Task.CompletedTask;

        // Max 策略累加
        if (evt.Usage != null)
        {
            if (evt.Usage.InputTokens > _inputTokens) _inputTokens = evt.Usage.InputTokens;
            if (evt.Usage.OutputTokens > _outputTokens) _outputTokens = evt.Usage.OutputTokens;
            if (evt.Usage.CacheReadTokens > _cacheReadTokens) _cacheReadTokens = evt.Usage.CacheReadTokens;
            if (evt.Usage.CacheCreationTokens > _cacheCreationTokens) _cacheCreationTokens = evt.Usage.CacheCreationTokens;
        }
        if (!string.IsNullOrEmpty(evt.ModelId)) _modelId = evt.ModelId;

        // 在完成事件上附带累积的 Usage
        if (evt.IsComplete)
        {
            evt.Usage = new ResponseUsage(_inputTokens, _outputTokens, _cacheReadTokens, _cacheCreationTokens);
            evt.ModelId = _modelId;
        }

        return Task.CompletedTask;
    }
}

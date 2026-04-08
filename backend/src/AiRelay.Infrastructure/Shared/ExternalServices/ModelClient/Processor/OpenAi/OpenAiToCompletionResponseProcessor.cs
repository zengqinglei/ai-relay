using System.Text;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi.Converter;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAi;

/// <summary>
/// Responses API → Chat Completions 格式转换 Processor
/// 仅在 down.RelativePath 包含 /chat/completions 时激活
/// 保留 OriginalBytes（上游原始数据），设置 ConvertedBytes（转换后数据）
/// 转换后的行各自追加 \n\n，空行及无转换结果一律不转发
/// </summary>
public class OpenAiToCompletionResponseProcessor(DownRequestContext down) : IResponseProcessor
{
    private readonly bool _isActive = down.IsStreaming
        && down.RelativePath.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase);
    private readonly ResponsesToCompletionsConverter _converter = new(
        down.ExtractedProps.TryGetValue("stream_options.include_usage", out var iuVal) && iuVal == "true");

    public bool RequiresMutation => _isActive;

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (!_isActive) return Task.CompletedTask;
        if (evt.Type == StreamEventType.Error) return Task.CompletedTask;

        // 保存原始字节（如果尚未设置）
        if (evt.OriginalBytes == null && !string.IsNullOrEmpty(evt.SseLine))
        {
            evt.OriginalBytes = Encoding.UTF8.GetBytes(evt.SseLine + "\n\n");
        }

        // 转换模式下接管所有 ConvertedBytes 控制：
        // 空行、无法转换的行一律清空 ConvertedBytes，不直接透传
        if (string.IsNullOrEmpty(evt.SseLine))
        {
            // 合成流结束事件（IsComplete=true）：若转换器尚未完成（上游异常断流），补发 finish chunk 及 [DONE]
            if (evt.IsComplete && !_converter.IsFinalized)
            {
                var sb = new StringBuilder();
                foreach (var line in _converter.Finalize())
                {
                    sb.Append(line);
                    sb.Append("\n\n");
                }
                sb.Append("data: [DONE]\n\n");
                evt.ConvertedBytes = Encoding.UTF8.GetBytes(sb.ToString());
            }
            else
            {
                evt.OriginalBytes = null;
                evt.ConvertedBytes = null;
            }
            return Task.CompletedTask;
        }

        var convertedLines = _converter.ConvertSseLine(evt.SseLine).ToList();
        if (convertedLines.Count > 0)
        {
            // 与改造前一致：每个转换行单独追加 \n\n（SSE 事件定界符）
            var sb = new StringBuilder();
            foreach (var line in convertedLines)
            {
                sb.Append(line);
                sb.Append("\n\n");
            }
            evt.ConvertedBytes = Encoding.UTF8.GetBytes(sb.ToString());
        }
        else
        {
            // 无转换结果（如 event: 类行）：同时清空 OriginalBytes 和 ConvertedBytes，不透传原始行
            evt.OriginalBytes = null;
            evt.ConvertedBytes = null;
        }

        return Task.CompletedTask;
    }
}

using System.Text;
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
public class OpenAiToCompletionResponseProcessor : IResponseProcessor
{
    private readonly ResponsesToCompletionsConverter _converter = new();

    public bool RequiresMutation => true;

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
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
            evt.OriginalBytes = null;
            evt.ConvertedBytes = null;
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
            // 无转换结果（如 event: 类行）：清空 ConvertedBytes，不透传原始行
            evt.ConvertedBytes = null;
        }

        return Task.CompletedTask;
    }
}

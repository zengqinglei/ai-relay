using System.Text;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Response.OpenAi;

/// <summary>
/// Responses API → Chat Completions 格式转换 Processor
/// 仅在 down.RelativePath 包含 /chat/completions 时激活
/// 接管 ForwardBytes 的完整控制：转换后的行各自追加 \n\n，空行及无转换结果一律不转发
/// </summary>
public class ToCompletionResponseProcessor : IResponseProcessor
{
    private readonly ResponsesToCompletionsConverter _converter = new();

    public bool RequiresMutation => true;

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (evt.Type == StreamEventType.Error) return Task.CompletedTask;

        // 转换模式下接管所有 ForwardBytes 控制：
        // 空行、无法转换的行一律清空 ForwardBytes，不直接透传
        if (string.IsNullOrEmpty(evt.SseLine))
        {
            evt.ForwardBytes = null;
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
            evt.ForwardBytes = Encoding.UTF8.GetBytes(sb.ToString());
        }
        else
        {
            // 无转换结果（如 event: 类行）：清空 ForwardBytes，不透传原始行
            evt.ForwardBytes = null;
        }

        return Task.CompletedTask;
    }
}

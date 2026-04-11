using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAiCompatible;

/// <summary>
/// OpenAI Compatible 请求体处理器
/// 主要负责模型名称映射
/// </summary>
public class OpenAiCompatibleModifyBodyRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    private readonly ChatModelConnectionOptions _options = options;

    public async Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.SessionId = down.SessionId;

        // 如果没有映射需求，直接返回，走流式转发
        if (string.IsNullOrEmpty(up.MappedModelId) || up.MappedModelId == down.ModelId)
        {
            return;
        }

        var clonedBody = await up.EnsureMutableBodyAsync(down);
        clonedBody["model"] = up.MappedModelId;
    }
}

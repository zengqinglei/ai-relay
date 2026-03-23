using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;

/// <summary>
/// OpenAI API Key 模式请求体处理器（透传，仅写入 mapped model）
/// </summary>
public class OpenAiApiKeyRequestBodyProcessor() : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var requestJson = down.CloneBodyJson();
        if (requestJson != null && !string.IsNullOrEmpty(up.MappedModelId) &&
            up.MappedModelId != down.ModelId)
        {
            requestJson["model"] = up.MappedModelId;
        }

        up.BodyJson = requestJson;
        up.SessionId = down.SessionHash;
        return Task.CompletedTask;
    }
}

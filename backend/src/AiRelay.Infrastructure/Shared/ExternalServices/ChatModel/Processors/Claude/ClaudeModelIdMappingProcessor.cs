using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

public class ClaudeModelIdMappingProcessor(IModelProvider modelProvider) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var modelId = down.ModelId ?? string.Empty;
        up.MappedModelId = modelProvider.GetClaudeMappedModel(modelId);
        return Task.CompletedTask;
    }
}

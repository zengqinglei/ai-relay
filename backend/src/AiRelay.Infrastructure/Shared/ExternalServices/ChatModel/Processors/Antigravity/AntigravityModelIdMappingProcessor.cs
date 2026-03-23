using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Antigravity;

public class AntigravityModelIdMappingProcessor(IModelProvider modelProvider) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var modelId = string.IsNullOrEmpty(down.ModelId) ? "gemini-2.0-flash-exp" : down.ModelId;
        up.MappedModelId = modelProvider.GetAntigravityMappedModel(modelId);
        return Task.CompletedTask;
    }
}

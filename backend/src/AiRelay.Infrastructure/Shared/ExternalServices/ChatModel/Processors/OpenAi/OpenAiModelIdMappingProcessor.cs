using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;

public class OpenAiModelIdMappingProcessor(IModelProvider modelProvider) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var modelId = string.IsNullOrEmpty(down.ModelId) ? "gpt-4o" : down.ModelId;
        up.MappedModelId = modelProvider.GetOpenAIMappedModel(modelId);
        return Task.CompletedTask;
    }
}

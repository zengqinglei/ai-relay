using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;

public class OpenAiModelIdMappingProcessor(IModelProvider modelProvider) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(down.ModelId))
        {
            return Task.CompletedTask;
        }

        up.MappedModelId = modelProvider.GetOpenAIMappedModel(down.ModelId);
        return Task.CompletedTask;
    }
}

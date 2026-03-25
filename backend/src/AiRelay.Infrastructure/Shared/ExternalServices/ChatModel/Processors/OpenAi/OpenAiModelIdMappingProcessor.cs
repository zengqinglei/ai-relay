using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;

public class OpenAiModelIdMappingProcessor(
    IModelProvider modelProvider,
    ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(down.ModelId))
            return Task.CompletedTask;

        // 1. 账户级映射优先
        var accountMapping = options.ModelMapping;
        if (accountMapping != null)
        {
            var mapped = AccountTokenDomainService.ResolveMapping(down.ModelId, accountMapping);
            if (mapped != null)
            {
                up.MappedModelId = mapped;
                return Task.CompletedTask;
            }
        }

        // 2. 平台级映射兜底
        up.MappedModelId = modelProvider.GetOpenAIMappedModel(down.ModelId);
        return Task.CompletedTask;
    }
}

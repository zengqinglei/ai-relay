using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Common;

/// <summary>
/// 通用模型 ID 映射处理器
/// 取代各平台各自独立的 XxxModelIdMappingRequestProcessor（逻辑完全一致，仅平台映射表不同）
/// </summary>
/// <param name="modelProvider">模型提供者服务，通过 GetMappedModel(Provider, string) 按平台分发</param>
/// <param name="provider">当前账户的平台标识，由 ChatModelConnectionOptions.Provider 传入</param>
/// <param name="options">当前账户连接配置，用于读取账户级 ModelMapping 规则</param>
public class ModelIdMappingRequestProcessor(
    IModelProvider modelProvider,
    Provider provider,
    ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(down.ModelId))
            return Task.CompletedTask;

        up.MappedModelId = AccountTokenDomainService.ResolveUpModelId(
            down.ModelId,
            provider,
            options.ModelMapping,
            modelProvider);

        return Task.CompletedTask;
    }
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using Leistd.Exception.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public class ChatModelHandlerFactory(IServiceProvider serviceProvider) : IChatModelHandlerFactory
{
    public IChatModelHandler CreateHandler(ProviderPlatform platform, string accessToken, string? baseUrl = null, Dictionary<string, string>? extraProperties = null, bool shouldMimicOfficialClient = true)
    {
        var options = new ChatModelConnectionOptions(
            Platform: platform,
            Credential: accessToken,
            BaseUrl: baseUrl)
        {
            ShouldMimicOfficialClient = shouldMimicOfficialClient,
            ExtraProperties = extraProperties ?? new Dictionary<string, string>()
        };

        var client = CreateHandler(platform);
        client.Configure(options);
        return client;
    }

    public IChatModelHandler CreateHandler(ProviderPlatform platform)
    {
        var clients = serviceProvider.GetServices<IChatModelHandler>();
        return clients.FirstOrDefault(c => c.Supports(platform))
            ?? throw new NotFoundException($"不支持的平台类型: {platform}");
    }
}

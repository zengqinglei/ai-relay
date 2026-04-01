using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Leistd.Exception.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

public class ChatModelHandlerFactory(IServiceProvider serviceProvider) : IChatModelHandlerFactory
{
    private static readonly Dictionary<ProviderPlatform, Type> HandlerTypes = new()
    {
        [ProviderPlatform.CLAUDE_OAUTH]  = typeof(ClaudeChatModelHandler),
        [ProviderPlatform.CLAUDE_APIKEY] = typeof(ClaudeChatModelHandler),
        [ProviderPlatform.OPENAI_OAUTH]  = typeof(OpenAiChatModelHandler),
        [ProviderPlatform.OPENAI_APIKEY] = typeof(OpenAiChatModelHandler),
        [ProviderPlatform.ANTIGRAVITY]   = typeof(AntigravityChatModelHandler),
        [ProviderPlatform.GEMINI_OAUTH]  = typeof(GeminiAccountChatModelHandler),
        [ProviderPlatform.GEMINI_APIKEY] = typeof(GeminiApiChatModelHandler),
    };

    /// <summary>
    /// 创建携带凭证的 Handler（代理入口 / 测试入口共用）
    /// options 通过构造函数注入，替代旧的 Configure()
    /// </summary>
    public IChatModelHandler CreateHandler(
        ProviderPlatform platform,
        string accessToken,
        string? baseUrl = null,
        Dictionary<string, string>? extraProperties = null,
        bool shouldMimicOfficialClient = true,
        List<string>? modelWhites = null,
        Dictionary<string, string>? modelMapping = null)
    {
        var options = new ChatModelConnectionOptions(
            Platform: platform,
            Credential: accessToken,
            BaseUrl: baseUrl)
        {
            ShouldMimicOfficialClient = shouldMimicOfficialClient,
            ExtraProperties = extraProperties ?? new Dictionary<string, string>(),
            ModelWhites = modelWhites,
            ModelMapping = modelMapping
        };

        return CreateHandlerWithOptions(platform, options);
    }

    /// <summary>
    /// 仅用于 ExtractModelInfo 路由判断（无凭证）
    /// </summary>
    public IChatModelHandler CreateHandler(ProviderPlatform platform)
    {
        var emptyOptions = new ChatModelConnectionOptions(platform, string.Empty);
        return CreateHandlerWithOptions(platform, emptyOptions);
    }

    private IChatModelHandler CreateHandlerWithOptions(ProviderPlatform platform, ChatModelConnectionOptions options)
    {
        if (!HandlerTypes.TryGetValue(platform, out var handlerType))
            throw new NotFoundException($"不支持的平台类型: {platform}");

        return (IChatModelHandler)ActivatorUtilities.CreateInstance(serviceProvider, handlerType, options);
    }
}

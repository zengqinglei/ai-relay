using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Leistd.Exception.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient;

public class ChatModelHandlerFactory(IServiceProvider serviceProvider) : IChatModelHandlerFactory
{
    private static readonly Dictionary<(Provider, AuthMethod), Type> HandlerTypes = new()
    {
        [(Provider.Claude, AuthMethod.OAuth)]  = typeof(ClaudeChatModelHandler),
        [(Provider.Claude, AuthMethod.ApiKey)] = typeof(ClaudeChatModelHandler),
        [(Provider.OpenAI, AuthMethod.OAuth)]  = typeof(OpenAiChatModelHandler),
        [(Provider.OpenAI, AuthMethod.ApiKey)] = typeof(OpenAiChatModelHandler),
        [(Provider.Antigravity, AuthMethod.OAuth)]   = typeof(AntigravityChatModelHandler),
        [(Provider.Gemini, AuthMethod.OAuth)]  = typeof(GeminiAccountChatModelHandler),
        [(Provider.Gemini, AuthMethod.ApiKey)] = typeof(GeminiApiChatModelHandler),
        [(Provider.OpenAICompatible, AuthMethod.ApiKey)] = typeof(OpenAiCompatibleChatModelHandler),
    };

    /// <summary>
    /// 创建携带凭证的 Handler（代理入口 / 测试入口共用）
    /// options 通过构造函数注入，替代旧的 Configure()
    /// </summary>
    public IChatModelHandler CreateHandler(
        Provider provider,
        AuthMethod authMethod,
        string accessToken,
        string? baseUrl = null,
        Dictionary<string, string>? extraProperties = null,
        bool shouldMimicOfficialClient = true,
        List<string>? modelWhites = null,
        Dictionary<string, string>? modelMapping = null)
    {
        var options = new ChatModelConnectionOptions(
            Provider: provider,
            AuthMethod: authMethod,
            Credential: accessToken,
            BaseUrl: baseUrl)
        {
            ShouldMimicOfficialClient = shouldMimicOfficialClient,
            ExtraProperties = extraProperties ?? new Dictionary<string, string>(),
            ModelWhites = modelWhites,
            ModelMapping = modelMapping
        };

        return CreateHandlerWithOptions(provider, authMethod, options);
    }

    /// <summary>
    /// 仅用于 ExtractModelInfo 路由判断（无凭证）
    /// </summary>
    public IChatModelHandler CreateHandler(Provider provider, AuthMethod authMethod)
    {
        var emptyOptions = new ChatModelConnectionOptions(provider, authMethod, string.Empty);
        return CreateHandlerWithOptions(provider, authMethod, emptyOptions);
    }

    private IChatModelHandler CreateHandlerWithOptions(Provider provider, AuthMethod authMethod, ChatModelConnectionOptions options)
    {
        if (!HandlerTypes.TryGetValue((provider, authMethod), out var handlerType))
            throw new NotFoundException($"不支持的提供商/认证组合: {provider} - {authMethod}");

        return (IChatModelHandler)ActivatorUtilities.CreateInstance(serviceProvider, handlerType, options);
    }

    public IChatModelHandler CreateHandler(RouteProfile routeProfile)
    {
        // 主要是为获取对应的 Handler 的 Downstream 解析逻辑，所以 AuthMethod 无论是 OAuth 还是 ApiKey 都行。
        var (provider, authMethod) = routeProfile switch
        {
            RouteProfile.GeminiInternal => (Provider.Gemini, AuthMethod.OAuth),
            RouteProfile.GeminiBeta => (Provider.Gemini, AuthMethod.ApiKey),
            RouteProfile.OpenAiResponses => (Provider.OpenAI, AuthMethod.ApiKey),
            RouteProfile.OpenAiCodex => (Provider.OpenAI, AuthMethod.OAuth),
            RouteProfile.ChatCompletions => (Provider.OpenAI, AuthMethod.ApiKey),
            RouteProfile.ClaudeMessages => (Provider.Claude, AuthMethod.ApiKey),
            _ => throw new ArgumentOutOfRangeException(nameof(routeProfile), routeProfile, null)
        };

        return CreateHandler(provider, authMethod);
    }

}

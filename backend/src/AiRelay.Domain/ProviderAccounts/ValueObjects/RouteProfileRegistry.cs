namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

public class RouteProfileDefinition
{
    public string PathPrefix { get; }
    public IReadOnlyList<(Provider Provider, AuthMethod AuthMethod)> SupportedCombinations { get; }

    public RouteProfileDefinition(string pathPrefix, IReadOnlyList<(Provider, AuthMethod)> supportedCombinations)
    {
        PathPrefix = pathPrefix;
        SupportedCombinations = supportedCombinations;
    }
}

public static class RouteProfileRegistry
{
    public static readonly Dictionary<RouteProfile, RouteProfileDefinition> Profiles = new()
    {
        [RouteProfile.GeminiInternal] = new RouteProfileDefinition("/v1internal", 
            [
                (Provider.Gemini, AuthMethod.OAuth)
            ]),

        [RouteProfile.GeminiBeta] = new RouteProfileDefinition("/v1beta",
            [
                (Provider.Gemini, AuthMethod.OAuth),
                (Provider.Gemini, AuthMethod.ApiKey),
                (Provider.Antigravity, AuthMethod.OAuth)
            ]),

        [RouteProfile.OpenAiResponses] = new RouteProfileDefinition("/v1/responses",
            [
                (Provider.OpenAI, AuthMethod.OAuth),
                (Provider.OpenAI, AuthMethod.ApiKey)
            ]),

        [RouteProfile.OpenAiCodex] = new RouteProfileDefinition("/backend-api/codex",
            [
                (Provider.OpenAI, AuthMethod.OAuth)
            ]),

        [RouteProfile.ChatCompletions] = new RouteProfileDefinition("/v1/chat/completions",
            [
                (Provider.OpenAI, AuthMethod.OAuth),
                (Provider.OpenAI, AuthMethod.ApiKey),
                (Provider.OpenAICompatible, AuthMethod.ApiKey)
            ]),

        [RouteProfile.ClaudeMessages] = new RouteProfileDefinition("/v1/messages",
            [
                (Provider.Claude, AuthMethod.OAuth),
                (Provider.Claude, AuthMethod.ApiKey),
                (Provider.Antigravity, AuthMethod.OAuth)
            ])
    };
}

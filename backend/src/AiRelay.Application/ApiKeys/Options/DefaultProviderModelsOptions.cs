namespace AiRelay.Application.ApiKeys.Options;

public class DefaultProviderModelsOptions
{
    public const string SectionName = "DefaultProviderModels";

    public string ProviderIdPrefix { get; set; } = "ai-relay";

    public string[] Models { get; set; } =
    [
        "glm-5.1",
        "qwen3.5-plus",
        "minimax-m2.7"
    ];
}

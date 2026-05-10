namespace AiRelay.Application.ApiKeys.Options;

public class DefaultProviderModelsOptions
{
    public const string SectionName = "DefaultProviderModels";

    public string ProviderIdPrefix { get; set; } = "ai-relay";

    public string[] Models { get; set; } =
    [
        "qwen3.5-plus",
        "glm-5",
        "glm-5.1",
        "minimax-m2.5",
        "minimax-m2.7"
    ];
}

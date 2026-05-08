namespace AiRelay.Application.ApiKeys.Dtos;

public class DefaultProviderModelsOutputDto
{
    public required DefaultApiKeyOutputDto ApiKey { get; init; }
    public required IReadOnlyList<DefaultProviderModelEndpointOutputDto> Endpoints { get; init; }
}

public class DefaultApiKeyOutputDto
{
    public required string Name { get; init; }
    public required string Secret { get; init; }
}

public class DefaultProviderModelEndpointOutputDto
{
    public required string Id { get; init; }
    public required string Protocol { get; init; }
    public required string BaseUrl { get; init; }
    public IReadOnlyList<string> Models { get; init; } = [];
}

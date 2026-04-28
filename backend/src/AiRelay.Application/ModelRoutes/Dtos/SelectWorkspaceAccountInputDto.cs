namespace AiRelay.Application.ModelRoutes.Dtos;

public record SelectWorkspaceAccountInputDto
{
    public required Guid UserId { get; init; }

    public required Guid SessionId { get; init; }

    public Guid? ProviderGroupId { get; init; }

    public Guid? AccountId { get; init; }

    public required string ModelId { get; init; }
}

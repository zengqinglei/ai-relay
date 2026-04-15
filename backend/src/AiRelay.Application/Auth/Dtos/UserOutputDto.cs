namespace AiRelay.Application.Auth.Dtos;

public record UserOutputDto
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public string? Nickname { get; init; }
    public string? Avatar { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreationTime { get; init; }
    public required string[] Roles { get; init; }
}

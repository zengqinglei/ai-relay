namespace AiRelay.Application.Users.Dtos;

/// <summary>
/// 用户管理输出 DTO
/// </summary>
public record UserManagementOutputDto
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? Avatar { get; init; }
    public bool IsActive { get; init; }
    public bool IsEmailVerified { get; init; }
    public required string[] Roles { get; init; }
    public bool IsSuperAdmin { get; init; }
    public DateTime CreationTime { get; init; }
    public DateTime? LastLoginTime { get; init; }
}

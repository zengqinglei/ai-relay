namespace AiRelay.Application.OpenApplications.Dtos;

/// <summary>
/// 重置开放应用密钥输出 DTO
/// </summary>
public record ResetOpenApplicationSecretOutputDto
{
    /// <summary>
    /// Client Secret
    /// </summary>
    public required string ClientSecret { get; init; }
}

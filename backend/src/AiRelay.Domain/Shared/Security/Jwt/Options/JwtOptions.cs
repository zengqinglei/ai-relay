namespace AiRelay.Domain.Shared.Security.Jwt.Options;

/// <summary>
/// JWT 配置选项
/// </summary>
public record JwtOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// 密钥
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// 发行者
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// 受众
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>
    /// 过期时间（分钟）
    /// </summary>
    public int ExpiryMinutes { get; init; } = 10080;
}

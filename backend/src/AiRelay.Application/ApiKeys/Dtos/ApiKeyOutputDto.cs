namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// API Key 输出 DTO
/// </summary>
public record ApiKeyOutputDto
{
    /// <summary>
    /// ID
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 密钥（⚠️ 创建时返回明文，查询时通过 AutoMapper 解密返回）
    /// </summary>
    public required string Secret { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public required DateTime CreationTime { get; init; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime? LastUsedAt { get; init; }

    /// <summary>
    /// 今日调用次数
    /// </summary>
    public long UsageToday { get; set; }

    /// <summary>
    /// 总调用次数
    /// </summary>
    public long UsageTotal { get; set; }

    /// <summary>
    /// 绑定分组列表
    /// </summary>
    public List<ApiKeyBindingOutputDto> Bindings { get; set; } = new();
}

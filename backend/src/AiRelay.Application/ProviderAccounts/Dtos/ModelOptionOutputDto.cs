namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 模型选项输出 DTO
/// </summary>
public record ModelOptionOutputDto
{
    /// <summary>
    /// 显示标签
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// 模型值
    /// </summary>
    public string Value { get; init; } = string.Empty;
}

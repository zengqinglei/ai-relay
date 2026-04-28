using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;

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

    /// <summary>
    /// 模型分类
    /// </summary>
    public ModelCategory Category { get; init; } = ModelCategory.Chat;

    /// <summary>
    /// 模型目录归属/厂商
    /// </summary>
    public ModelVendor? Vendor { get; init; }
}

namespace AiRelay.Domain.UsageRecords.Options;

/// <summary>
/// 模型定价配置选项
/// </summary>
public class ModelPricingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ModelPricing";

    /// <summary>
    /// 远程定价数据 URL
    /// </summary>
    public string? RemoteUrl { get; set; }
}

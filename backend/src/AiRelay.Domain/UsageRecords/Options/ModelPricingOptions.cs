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

    /// <summary>
    /// 本地备份文件绝对路径（不配置时由宿主层注入默认值）
    /// </summary>
    public string? LocalPath { get; set; }
}

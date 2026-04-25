namespace AiRelay.Domain.UsageRecords.Options;

/// <summary>
/// 使用记录自动清理配置
/// </summary>
public class UsageCleanupOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "UsageCleanup";

    /// <summary>
    /// 是否启用自动清理，默认 true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 摘要元数据保留天数（UsageRecord / UsageRecordAttempt）
    /// 默认 90 天
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// 重型 Body 详情保留天数（UsageRecordDetail / UsageRecordAttemptDetail）
    /// 默认 7 天；建议不超过 RetentionDays
    /// </summary>
    public int DetailRetentionDays { get; set; } = 7;
}

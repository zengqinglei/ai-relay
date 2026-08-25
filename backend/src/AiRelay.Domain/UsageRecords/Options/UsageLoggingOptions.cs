namespace AiRelay.Domain.UsageRecords.Options;

/// <summary>
/// 使用记录日志配置选项
/// </summary>
public class UsageLoggingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "UsageLogging";

    /// <summary>
    /// 是否开启 Body 记录
    /// </summary>
    public bool IsBodyLoggingEnabled { get; set; } = false;

    /// <summary>
    /// Body 最大记录长度 (字符数)。控制请求/响应体日志的截断上限。
    /// 注意：下游 Body 预览还受 JsonExtractHelper 的 maxBytesToRead (1MB) 约束。
    /// </summary>
    public int MaxBodyLength { get; set; } = 1024 * 128; // 128KB

    /// <summary>
    /// 排除的Content-Type列表
    /// </summary>
    public List<string> ExcludeContentTypes { get; set; } = new()
    {
        "multipart/form-data",
        "application/octet-stream",
        "image/",
        "video/",
        "audio/"
    };
}

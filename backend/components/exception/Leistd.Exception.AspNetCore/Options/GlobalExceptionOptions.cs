namespace Leistd.Exception.AspNetCore.Options;

/// <summary>
/// 全局异常配置
/// </summary>
public class GlobalExceptionOptions
{
    /// <summary>
    /// 是否启用全局异常处理
    /// </summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// 排除的URI模式（如：/api/health/**）
    /// </summary>
    public HashSet<string> ExcludePatterns { get; set; } = [];

    /// <summary>
    /// 是否显示详情，null表示根据环境自动决定（开发环境显示）
    /// </summary>
    public bool? IsShowDetails { get; set; }
}

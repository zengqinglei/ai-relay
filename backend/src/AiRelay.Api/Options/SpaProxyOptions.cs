namespace AiRelay.Api.Options;

/// <summary>
/// SPA 开发代理配置选项
/// </summary>
public class SpaProxyOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "SpaProxy";

    /// <summary>
    /// 是否启用 SPA 开发代理
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// SPA 开发服务器地址
    /// </summary>
    public string? Target { get; set; }
}

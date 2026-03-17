namespace Leistd.Tracing.Core.Options;

public class CorrelationIdOptions
{
    /// <summary>
    /// 是否启用链路追踪
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// HTTP Header 名称 (默认: X-Correlation-Id)。
    /// 支持多个 Header，使用逗号分隔，例如: "X-Correlation-Id,X-Request-Id"
    /// </summary>
    public string HttpHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// 获取解析后的 Header 名称列表
    /// </summary>
    public string[] GetHttpHeaderNames()
    {
        return HttpHeaderName?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               ?? Array.Empty<string>();
    }

    /// <summary>
    /// 是否将 TraceId 回写到响应头
    /// </summary>
    public bool SetResponseHeader { get; set; } = true;
}
using System.Text;
using System.Text.RegularExpressions;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Helpers;

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;

/// <summary>
/// 下游请求上下文（客户端 → 网关）
/// 封装纯技术信息，不包含业务属性
/// 优化设计：全流式处理，零分配，禁止 LOH 驻留
/// </summary>
public class DownRequestContext
{
    // ============ 请求端点信息 ============
    public HttpMethod Method { get; init; } = HttpMethod.Post;
    public string RelativePath { get; init; } = string.Empty;
    public string? QueryString { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    // ============ 原始数据域 ============
    /// <summary>
    /// 原始求体流（已开启 Buffering，随时可重置 Position=0 读取）
    /// 支持零分配网络直通（Streaming Network Pass-through）
    /// </summary>
    public Stream? RawStream { get; init; }

    /// <summary>
    /// 是否为 multipart 请求
    /// </summary>
    public bool IsMultipart { get; init; }

    // ============ 从拦截源懒加载一次性提取的字典缓存 ============
    public IReadOnlyDictionary<string, string> ExtractedProps { get; init; } = new Dictionary<string, string>();
    public string? PreloadedBodyPreview { get; init; }

    /// <summary>
    /// 是否为流式响应（从各个可能位置嗅探流标志）
    /// </summary>
    public bool IsStreaming
    {
        get
        {
            if (_isStreaming.HasValue) return _isStreaming.Value;

            // 1. 检查 QueryString
            if (QueryString?.Contains("stream=true", StringComparison.OrdinalIgnoreCase) == true)
            {
                _isStreaming = true;
                return true;
            }

            // 2. 检查 URL 路径（Gemini API: :streamGenerateContent）
            if (RelativePath?.Contains(":streamGenerateContent", StringComparison.OrdinalIgnoreCase) == true)
            {
                _isStreaming = true;
                return true;
            }

            // 3. 检查 Header（X-Stainless-Helper-Method: stream）
            if (Headers.TryGetValue("X-Stainless-Helper-Method", out var helperMethod) &&
                helperMethod.Equals("stream", StringComparison.OrdinalIgnoreCase))
            {
                _isStreaming = true;
                return true;
            }

            // 4. 检查解析好的特征字典
            if (ExtractedProps.TryGetValue("stream", out var streamStr) &&
                bool.TryParse(streamStr, out var isStream) && isStream)
            {
                _isStreaming = true;
                return true;
            }

            _isStreaming = false;
            return false;
        }
    }
    private bool? _isStreaming;


    // ============ 提取的业务特征信息（由 ExtractModelInfo 赋值） ============
    // 只提供简单属性！这些属性的灌扫发生在一开始。

    public string? ModelId { get; set; }
    public string? SessionId { get; set; }

    // 模拟指纹信息
    public int? PromptIndex { get; set; }
    public string? StickySessionId { get; set; }
    public string? FingerprintClientId { get; set; }

    // ============ 辅助方法 ============
    public string? GetUserAgent() => Headers.TryGetValue("user-agent", out var ua) ? ua : null;

}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

/// <summary>
/// 下游请求上下文（客户端 → 网关）
/// 封装纯技术信息，不包含业务属性
/// 优化设计：保留原始字节 + 懒加载解析，避免重复解析和序列化
/// </summary>
public class DownRequestContext
{
    // ============ 请求信息 ============
    public HttpMethod Method { get; init; } = HttpMethod.Post;
    public string RelativePath { get; init; } = string.Empty;
    public string? QueryString { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    // ============ 原始数据 ============
    /// <summary>
    /// 原始请求体字节（用于未修改时直接转发）
    /// </summary>
    public ReadOnlyMemory<byte> BodyBytes { get; init; }

    /// <summary>
    /// 是否为 multipart 请求
    /// </summary>
    public bool IsMultipart { get; init; }

    /// <summary>
    /// 是否为流式响应（懒加载，从 QueryString、URL 路径或 Body 检测）
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

            // 3. 检查 Body
            _isStreaming = BodyJsonNode is JsonObject jsonObj &&
                           jsonObj.TryGetPropertyValue("stream", out var streamNode) &&
                           streamNode is JsonValue streamValue &&
                           streamValue.TryGetValue<bool>(out var isStream) &&
                           isStream;
            return _isStreaming.Value;
        }
    }
    private bool? _isStreaming;

    // ============ 懒加载属性 ============
    private JsonNode? _bodyJsonNode;

    /// <summary>
    /// 获取 Body 内容（用于日志或正则匹配）
    /// maxLength = int.MaxValue 获取完整内容
    /// </summary>
    public string GetBodyPreview(int maxLength = 2000)
    {
        if (BodyBytes.Length == 0) return string.Empty;

        var length = Math.Min(BodyBytes.Length, maxLength);
        var content = Encoding.UTF8.GetString(BodyBytes[..length].Span);

        return BodyBytes.Length > maxLength ? content + "...[Truncated]" : content;
    }

    /// <summary>
    /// 在 Body 中搜索正则表达式（流式搜索，按需读取）
    /// 只读取必要的字节，找到匹配后立即返回
    /// </summary>
    public Match SearchBodyPattern(Regex regex, int maxSearchLength = 50000)
    {
        if (BodyBytes.Length == 0) return Match.Empty;

        var searchLength = Math.Min(BodyBytes.Length, maxSearchLength);
        var searchContent = Encoding.UTF8.GetString(BodyBytes[..searchLength].Span);

        return regex.Match(searchContent);
    }

    /// <summary>
    /// 请求体 JsonNode（懒加载，用于读取和修改）
    /// 警告：修改会影响缓存的实例，如需独立副本请使用 CloneBodyJson()
    /// </summary>
    public JsonNode? BodyJsonNode
    {
        get
        {
            if (_bodyJsonNode == null && BodyBytes.Length > 0)
            {
                try
                {
                    _bodyJsonNode = JsonNode.Parse(BodyBytes.Span);
                }
                catch (JsonException)
                {
                    // 非 JSON body，返回 null
                }
            }
            return _bodyJsonNode;
        }
    }

    /// <summary>
    /// 克隆 Body JSON（仅在需要修改时调用，避免不必要的 DeepClone）
    /// </summary>
    public JsonObject? CloneBodyJson()
    {
        return BodyJsonNode?.DeepClone() as JsonObject;
    }

    // ============ 提取的信息（由 ExtractModelInfo 填充） ============
    public string? ModelId { get; set; }
    public string? SessionId { get; set; }

    // 模拟指纹信息
    public int? PromptIndex { get; set; }
    public string? StickySessionId { get; set; }
    public string? FingerprintClientId { get; set; }

    // ============ 辅助方法 ============
    public string? GetUserAgent() => Headers.TryGetValue("user-agent", out var ua) ? ua : null;
}

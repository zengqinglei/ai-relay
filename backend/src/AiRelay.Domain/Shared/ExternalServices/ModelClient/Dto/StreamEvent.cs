using System.Text.Json.Serialization;

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

/// <summary>
/// 事件类型
/// </summary>
public enum StreamEventType
{
    /// <summary>正常内容</summary>
    Content,
    /// <summary>错误信息</summary>
    Error,
    /// <summary>系统调试信息</summary>
    System
}

/// <summary>
/// 统一响应事件模型
/// 使用 Type 判别联合：Content（默认）/ Error / System
/// </summary>
public class StreamEvent
{
    // ── 应用层字段（序列化给前端） ──

    /// <summary>事件类型</summary>
    public StreamEventType Type { get; set; } = StreamEventType.Content;

    /// <summary>文本内容（根据 Type 含义不同）</summary>
    public string? Content { get; set; }

    /// <summary>是否完成</summary>
    public bool IsComplete { get; set; }

    /// <summary>内联数据（如图片）</summary>
    public InlineDataPart? InlineData { get; set; }

    // ── 传输层字段（[JsonIgnore] 不序列化） ──

    /// <summary>
    /// 原始 SSE 行文本（ParseSse Processor 的输入，仅用于解析）
    /// 由 BaseChatModelHandler 读取 SSE 行时填充
    /// </summary>
    [JsonIgnore] public string? SseLine { get; set; }

    /// <summary>
    /// 原始字节（上游返回的原始数据，始终保存）
    /// 用于审计日志记录转换前的数据
    /// </summary>
    [JsonIgnore] public byte[]? OriginalBytes { get; set; }

    /// <summary>
    /// 转换后的字节（仅在经过转换处理器后才赋值，未转换时为 null）
    /// 转发时优先使用此字段，为 null 则使用 OriginalBytes
    /// 用于审计日志记录转换后的数据
    /// </summary>
    [JsonIgnore] public byte[]? ConvertedBytes { get; set; }

    /// <summary>解析后的 Usage（由 ParseSse Processor 填充）</summary>
    [JsonIgnore] public ResponseUsage? Usage { get; set; }

    /// <summary>
    /// 流监控探针：标记该事件是否携带真实的返回输出内容或工具意图
    /// </summary>
    [JsonIgnore] public bool HasOutput { get; set; }

    /// <summary>解析出的 ModelId</summary>
    [JsonIgnore] public string? ModelId { get; set; }
}



/// <summary>
/// 内联数据（图片等）
/// </summary>
public record InlineDataPart(
    string MimeType,
    string Data
);

/// <summary>
/// 响应中的 Token 使用量
/// </summary>
public record ResponseUsage(
    int InputTokens,
    int OutputTokens,
    int CacheReadTokens = 0,
    int CacheCreationTokens = 0
);

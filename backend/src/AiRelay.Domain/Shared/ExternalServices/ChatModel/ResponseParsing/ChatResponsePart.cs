namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

/// <summary>
/// 聊天响应部分
/// </summary>
/// <param name="Content">文本内容片段</param>
/// <param name="Usage">Token 使用量（增量或全量，取决于提供商）</param>
/// <param name="IsComplete">是否响应结束</param>
/// <param name="ModelId">模型 ID</param>
/// <param name="Error">错误信息</param>
/// <param name="InlineData">内联数据（如图片）</param>
public record ChatResponsePart(
    string? Content = null,
    ResponseUsage? Usage = null,
    bool IsComplete = false,
    string? ModelId = null,
    string? Error = null,
    InlineDataPart? InlineData = null
);

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

namespace AiRelay.Application.ChatSessions.Dtos;

/// <summary>
/// 聊天会话输出
/// </summary>
public class ChatSessionOutputDto
{
    /// <summary>会话 ID</summary>
    public Guid Id { get; set; }

    /// <summary>标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>资源池分组 ID</summary>
    public Guid? ProviderGroupId { get; set; }

    /// <summary>模型 ID</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>固定账户 ID</summary>
    public Guid? AccountId { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>最后消息时间</summary>
    public DateTime? LastMessageTime { get; set; }

    /// <summary>最后消息预览</summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>消息总数</summary>
    public int MessageCount { get; set; }
}

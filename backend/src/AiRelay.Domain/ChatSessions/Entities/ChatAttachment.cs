using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ChatSessions.Entities;

/// <summary>
/// 聊天消息附件
/// </summary>
public class ChatAttachment : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// 消息 ID
    /// </summary>
    public Guid MessageId { get; private set; }

    /// <summary>
    /// MIME 类型
    /// </summary>
    public string MimeType { get; private set; }

    /// <summary>
    /// Base64 数据
    /// </summary>
    public string? Data { get; private set; }

    /// <summary>
    /// 外部 URL
    /// </summary>
    public string? Url { get; private set; }

    private ChatAttachment() => MimeType = null!;

    public ChatAttachment(Guid messageId, string mimeType, string? data = null, string? url = null)
    {
        Id = Guid.CreateVersion7();
        MessageId = messageId;
        MimeType = mimeType;
        Data = data;
        Url = url;
    }
}

using AiRelay.Domain.ChatSessions.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ChatSessions.Entities;

/// <summary>
/// 聊天消息实体
/// </summary>
public class ChatMessage : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// 消息角色
    /// </summary>
    public ChatMessageRole Role { get; private set; }

    /// <summary>
    /// 文本内容
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// 思考/推理过程文本
    /// </summary>
    public string? ReasoningContent { get; private set; }

    /// <summary>
    /// 附件列表
    /// </summary>
    public virtual ICollection<ChatAttachment> Attachments { get; private set; } = new List<ChatAttachment>();

    private ChatMessage() => Content = null!;

    public ChatMessage(Guid sessionId, ChatMessageRole role, string content, string? reasoningContent = null)
    {
        Id = Guid.CreateVersion7();
        SessionId = sessionId;
        Role = role;
        Content = content;
        ReasoningContent = string.IsNullOrWhiteSpace(reasoningContent) ? null : reasoningContent;
    }

    /// <summary>
    /// 替换附件
    /// </summary>
    public void ReplaceAttachments(IReadOnlyCollection<InlineDataPart>? attachments)
    {
        Attachments.Clear();

        if (attachments == null || attachments.Count == 0)
        {
            return;
        }

        foreach (var attachment in attachments)
        {
            Attachments.Add(new ChatAttachment(
                Id,
                attachment.MimeType,
                attachment.Data,
                attachment.Url));
        }
    }
}

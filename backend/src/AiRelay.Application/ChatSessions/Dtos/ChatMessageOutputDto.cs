using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

namespace AiRelay.Application.ChatSessions.Dtos;

/// <summary>
/// 聊天消息输出
/// </summary>
public class ChatMessageOutputDto
{
    /// <summary>消息 ID</summary>
    public Guid Id { get; set; }

    /// <summary>会话 ID</summary>
    public Guid SessionId { get; set; }

    /// <summary>消息角色</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>文本内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>思考/推理过程文本</summary>
    public string? ReasoningContent { get; set; }

    /// <summary>附件列表</summary>
    public List<InlineDataPart>? Attachments { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>是否仍处于流式输出态</summary>
    public bool? IsStreaming { get; set; }
}

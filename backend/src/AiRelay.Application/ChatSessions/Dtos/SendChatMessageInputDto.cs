using System.ComponentModel.DataAnnotations;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

namespace AiRelay.Application.ChatSessions.Dtos;

/// <summary>
/// 发送聊天消息输入
/// </summary>
public class SendChatMessageInputDto
{
    /// <summary>文本内容</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>附件列表</summary>
    public List<InlineDataPart>? Attachments { get; set; }
}

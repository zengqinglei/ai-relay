using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ChatSessions.Dtos;

/// <summary>
/// 创建聊天会话输入
/// </summary>
public class CreateChatSessionInputDto
{
    /// <summary>标题</summary>
    public string? Title { get; set; }

    /// <summary>资源池分组 ID</summary>
    public Guid? ProviderGroupId { get; set; }

    /// <summary>模型 ID</summary>
    [Required]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>固定账户 ID</summary>
    public Guid? AccountId { get; set; }
}

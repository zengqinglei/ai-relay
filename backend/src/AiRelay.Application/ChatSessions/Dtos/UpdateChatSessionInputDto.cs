namespace AiRelay.Application.ChatSessions.Dtos;

/// <summary>
/// 更新聊天会话输入
/// </summary>
public class UpdateChatSessionInputDto
{
    /// <summary>标题</summary>
    public string? Title { get; set; }

    /// <summary>资源池分组 ID</summary>
    public Guid? ProviderGroupId { get; set; }

    /// <summary>是否切换为自动分组</summary>
    public bool UseAutoProviderGroup { get; set; }

    /// <summary>模型 ID</summary>
    public string? ModelId { get; set; }

    /// <summary>固定账户 ID</summary>
    public Guid? AccountId { get; set; }
}

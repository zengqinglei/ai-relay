namespace AiRelay.Application.ChatSessions.Dtos;

/// <summary>
/// 工作区聊天模型选项
/// </summary>
public class ChatModelOptionOutputDto
{
    /// <summary>显示名称</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>模型值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>推荐分组 ID</summary>
    public Guid? ProviderGroupId { get; set; }

    /// <summary>推荐分组名称</summary>
    public string? ProviderGroupName { get; set; }
}

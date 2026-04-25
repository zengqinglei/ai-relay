namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

/// <summary>
/// 统一聊天请求构造输入
/// </summary>
public sealed record ChatDownContextInput(
    string ModelId,
    string SessionId,
    IReadOnlyList<ChatDownContextMessageInput> Messages,
    bool Stream = true,
    int? MaxTokens = null,
    bool DisableStore = false);

/// <summary>
/// 聊天消息输入
/// </summary>
public sealed record ChatDownContextMessageInput(
    ChatDownContextMessageRole Role,
    string? Content,
    IReadOnlyList<ChatDownContextAttachmentInput>? Attachments = null);

/// <summary>
/// 聊天附件输入
/// </summary>
public sealed record ChatDownContextAttachmentInput(
    string MimeType,
    string? Url = null,
    string? Data = null);

/// <summary>
/// 聊天消息角色
/// </summary>
public enum ChatDownContextMessageRole
{
    System = 0,
    User = 1,
    Assistant = 2
}

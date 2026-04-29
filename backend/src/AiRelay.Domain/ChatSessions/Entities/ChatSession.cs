using AiRelay.Domain.ChatSessions.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ChatSessions.Entities;

/// <summary>
/// 工作区聊天会话
/// </summary>
public class ChatSession : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// 归属用户 ID
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// 资源池分组 ID
    /// </summary>
    public Guid? ProviderGroupId { get; private set; }

    /// <summary>
    /// 模型 ID
    /// </summary>
    public string ModelId { get; private set; }

    /// <summary>
    /// 固定账户 ID
    /// </summary>
    public Guid? AccountId { get; private set; }

    /// <summary>
    /// 最后消息时间
    /// </summary>
    public DateTime? LastMessageTime { get; private set; }

    /// <summary>
    /// 最后一条消息预览
    /// </summary>
    public string? LastMessagePreview { get; private set; }

    /// <summary>
    /// 消息总数
    /// </summary>
    public int MessageCount { get; private set; }

    /// <summary>
    /// 消息列表
    /// </summary>
    public virtual ICollection<ChatMessage> Messages { get; private set; } = new List<ChatMessage>();

    private ChatSession()
    {
        Title = null!;
        ModelId = null!;
    }

    public ChatSession(Guid userId, string title, Guid? providerGroupId, string modelId, Guid? accountId = null)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Title = string.IsNullOrWhiteSpace(title) ? "新会话" : title.Trim();
        ProviderGroupId = providerGroupId;
        ModelId = modelId;
        AccountId = accountId;
        LastMessageTime = DateTime.UtcNow;
        MessageCount = 0;
    }

    /// <summary>
    /// 更新会话设置
    /// </summary>
    public void Update(string? title = null, Guid? providerGroupId = null, string? modelId = null, Guid? accountId = null, bool useAutoProviderGroup = false)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }

        if (useAutoProviderGroup)
        {
            ProviderGroupId = null;
        }
        else if (providerGroupId.HasValue)
        {
            ProviderGroupId = providerGroupId.Value;
        }

        if (!string.IsNullOrWhiteSpace(modelId))
        {
            ModelId = modelId;
        }

        AccountId = accountId;
    }

    /// <summary>
    /// 添加用户消息
    /// </summary>
    public ChatMessage AddUserMessage(string content, IReadOnlyCollection<InlineDataPart>? attachments = null)
    {
        var message = AddMessage(ChatMessageRole.User, content, attachments);

        if (string.Equals(Title, "新会话", StringComparison.Ordinal))
        {
            Title = BuildTitle(content);
        }

        return message;
    }

    /// <summary>
    /// 添加助手消息（加入 Messages 集合，适用于 EF 级联保存场景）
    /// </summary>
    public ChatMessage AddAssistantMessage(
        string content,
        IReadOnlyCollection<InlineDataPart>? attachments = null,
        string? reasoningContent = null)
        => AddMessage(ChatMessageRole.Assistant, content, attachments, reasoningContent);

    /// <summary>
    /// 创建用户消息（不加入 Messages 集合，由调用方自行持久化消息实体）
    /// </summary>
    public ChatMessage CreateUserMessage(string content, IReadOnlyCollection<InlineDataPart>? attachments = null)
    {
        var message = CreateMessage(ChatMessageRole.User, content, attachments);

        if (string.Equals(Title, "新会话", StringComparison.Ordinal))
        {
            Title = BuildTitle(content);
        }

        return message;
    }

    /// <summary>
    /// 创建助手消息（不加入 Messages 集合，由调用方自行持久化消息实体）
    /// </summary>
    public ChatMessage CreateAssistantMessage(
        string content,
        IReadOnlyCollection<InlineDataPart>? attachments = null,
        string? reasoningContent = null)
        => CreateMessage(ChatMessageRole.Assistant, content, attachments, reasoningContent);

    private ChatMessage AddMessage(
        ChatMessageRole role,
        string content,
        IReadOnlyCollection<InlineDataPart>? attachments,
        string? reasoningContent = null)
    {
        var message = CreateMessage(role, content, attachments, reasoningContent);
        Messages.Add(message);
        return message;
    }

    private ChatMessage CreateMessage(
        ChatMessageRole role,
        string content,
        IReadOnlyCollection<InlineDataPart>? attachments,
        string? reasoningContent = null)
    {
        var message = new ChatMessage(Id, role, content, reasoningContent);
        message.ReplaceAttachments(attachments);
        MessageCount++;
        LastMessageTime = DateTime.UtcNow;
        LastMessagePreview = BuildPreview(content, attachments);
        return message;
    }

    private static string BuildTitle(string content)
    {
        var normalized = NormalizeText(content);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "新会话";
        }

        return normalized.Length > 20
            ? $"{normalized[..20]}..."
            : normalized;
    }

    private static string BuildPreview(string content, IReadOnlyCollection<InlineDataPart>? attachments)
    {
        var normalized = NormalizeText(content);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized.Length > 120
                ? $"{normalized[..120]}..."
                : normalized;
        }

        return attachments is { Count: > 0 } ? "[附件]" : string.Empty;
    }

    private static string NormalizeText(string content)
    {
        return string.Join(' ', content
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Trim();
    }
}

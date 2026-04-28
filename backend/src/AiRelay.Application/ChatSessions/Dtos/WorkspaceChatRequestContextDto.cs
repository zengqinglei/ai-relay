namespace AiRelay.Application.ChatSessions.Dtos;

/// <summary>
/// 工作区聊天下游请求上下文
/// </summary>
public sealed record WorkspaceChatRequestContextDto(
    IReadOnlyDictionary<string, string> Headers,
    string RequestUrl,
    string? ClientIp
);

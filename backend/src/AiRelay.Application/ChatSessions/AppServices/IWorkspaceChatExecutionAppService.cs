using AiRelay.Domain.ChatSessions.Entities;
using AiRelay.Application.ChatSessions.Dtos;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

namespace AiRelay.Application.ChatSessions.AppServices;

/// <summary>
/// 工作区聊天执行编排服务
/// </summary>
public interface IWorkspaceChatExecutionAppService
{
    /// <summary>
    /// 执行聊天请求并返回流式事件
    /// </summary>
    IAsyncEnumerable<StreamEvent> ExecuteAsync(
        ChatSession session,
        WorkspaceChatRequestContextDto requestContext,
        CancellationToken cancellationToken = default);
}

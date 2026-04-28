using AiRelay.Application.ChatSessions.Dtos;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.ChatSessions.AppServices;

/// <summary>
/// 工作区聊天会话应用服务
/// </summary>
public interface IChatSessionAppService
{
    Task<List<ChatSessionOutputDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<ChatSessionOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResultDto<ChatMessageOutputDto>> GetMessagePagedListAsync(
        Guid id,
        GetChatMessagePagedInputDto input,
        CancellationToken cancellationToken = default);

    Task<ChatSessionOutputDto> CreateAsync(CreateChatSessionInputDto input, CancellationToken cancellationToken = default);

    Task<ChatSessionOutputDto> UpdateAsync(Guid id, UpdateChatSessionInputDto input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatModelOptionOutputDto>> GetModelOptionsAsync(Guid? providerGroupId = null, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamEvent> SendMessageAsync(
        Guid id,
        SendChatMessageInputDto input,
        WorkspaceChatRequestContextDto requestContext,
        CancellationToken cancellationToken = default);
}

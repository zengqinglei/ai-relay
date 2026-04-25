using System.ComponentModel.DataAnnotations;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.ChatSessions.Dtos;

/// <summary>
/// 聊天消息分页查询输入
/// </summary>
public record GetChatMessagePagedInputDto : PagedRequestDto
{
    private const int DefaultChatMessageLimit = 30;

    public GetChatMessagePagedInputDto()
    {
        Limit = DefaultChatMessageLimit;
    }

    /// <summary>
    /// 向前分页游标消息 ID（为空时加载最新一页）
    /// </summary>
    [Display(Name = "游标消息")]
    public Guid? CursorMessageId { get; init; }
}

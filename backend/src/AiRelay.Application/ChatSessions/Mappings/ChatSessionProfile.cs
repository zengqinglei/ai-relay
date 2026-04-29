using AiRelay.Application.ChatSessions.Dtos;
using AiRelay.Domain.ChatSessions.Entities;
using AiRelay.Domain.ChatSessions.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Leistd.ObjectMapping.Mapster;

namespace AiRelay.Application.ChatSessions.Mappings;

/// <summary>
/// 工作区聊天会话映射配置
/// </summary>
public class ChatSessionProfile : MapsterProfile
{
    protected override void ConfigureMappings()
    {
        CreateMap<ChatAttachment, InlineDataPart>()
            .Map(dest => dest.MimeType, src => src.MimeType)
            .Map(dest => dest.Data, src => src.Data)
            .Map(dest => dest.Url, src => src.Url);

        CreateMap<ChatMessage, ChatMessageOutputDto>()
            .Map(dest => dest.Role, src => ResolveRole(src.Role))
            .Map(dest => dest.ReasoningContent, src => src.ReasoningContent)
            .Map(dest => dest.Attachments, src => src.Attachments.Count == 0 ? null : src.Attachments.ToList());

        CreateMap<ChatSession, ChatSessionOutputDto>();
    }

    private static string ResolveRole(ChatMessageRole role) => role switch
    {
        ChatMessageRole.User => "user",
        ChatMessageRole.Assistant => "assistant",
        _ => "system"
    };
}

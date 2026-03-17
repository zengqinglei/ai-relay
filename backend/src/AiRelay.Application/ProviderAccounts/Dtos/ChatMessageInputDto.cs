using System.ComponentModel.DataAnnotations;

namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 聊天消息输入 DTO
/// </summary>
public record ChatMessageInputDto
{
    /// <summary>
    /// 模型 ID
    /// </summary>
    [Required]
    public required string ModelId { get; init; }

    /// <summary>
    /// 消息内容
    /// </summary>
    [Required]
    public required string Message { get; init; }
}
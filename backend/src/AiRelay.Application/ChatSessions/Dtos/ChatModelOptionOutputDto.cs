using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;

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

    /// <summary>模型分类</summary>
    public ModelCategory Category { get; set; } = ModelCategory.Chat;

    /// <summary>模型目录归属/厂商</summary>
    public ModelVendor? Vendor { get; set; }

    /// <summary>推荐分组 ID</summary>
    public Guid? ProviderGroupId { get; set; }

    /// <summary>推荐分组名称</summary>
    public string? ProviderGroupName { get; set; }
}

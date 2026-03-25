using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

/// <summary>
/// 聊天流式响应事件
/// </summary>
/// <param name="Content">响应内容片段</param>
/// <param name="Error">错误信息</param>
/// <param name="SystemMessage">系统调试信息</param>
/// <param name="IsComplete">是否完成</param>
/// <param name="InlineData">内联数据（如图片）</param>
public record ChatStreamEvent(
    string? Content = null,
    string? Error = null,
    string? SystemMessage = null,
    bool IsComplete = false,
    InlineDataPart? InlineData = null
);

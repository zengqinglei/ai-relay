namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

/// <summary>
/// 模型选项（用于前端展示）
/// </summary>
/// <param name="Label">显示名称（如：Gemini 3 Flash）</param>
/// <param name="Value">实际值（如：gemini-3-flash）</param>
public record ModelOption(string Label, string Value);

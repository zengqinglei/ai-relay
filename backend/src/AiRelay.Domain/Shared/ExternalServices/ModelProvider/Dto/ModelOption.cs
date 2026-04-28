namespace AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;

public enum ModelCategory
{
    Chat,
    Image,
    Video,
    Audio,
    Embedding
}

/// <summary>
/// 模型选项（用于前端展示）
/// </summary>
/// <param name="Label">显示名称（如：Gemini 3 Flash）</param>
/// <param name="Value">实际值（如：gemini-3-flash）</param>
/// <param name="Category">模型分类</param>
/// <param name="Vendor">模型归属目录/厂商</param>
public record ModelOption(
    string Label,
    string Value,
    ModelCategory Category = ModelCategory.Chat,
    ModelVendor? Vendor = null);

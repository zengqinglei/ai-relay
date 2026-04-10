using System.Text.Json.Serialization;

namespace AiRelay.Domain.Shared.Json;

/// <summary>
/// 统一的 JSON 转换器配置
/// </summary>
public static class JsonConverters
{
    /// <summary>
    /// 字符串枚举转换器
    /// 将枚举序列化为字符串名称（而非整型值）
    /// 示例: Provider.Gemini → "Gemini"
    /// </summary>
    public static readonly JsonStringEnumConverter StringEnumConverter = new(
        namingPolicy: null,           // 保持原始枚举名称（不转换为 camelCase）
        allowIntegerValues: false     // 不允许整型值（强制字符串）
    );
}

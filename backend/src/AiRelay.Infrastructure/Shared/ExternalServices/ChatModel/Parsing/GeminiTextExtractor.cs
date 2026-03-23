using System.Text;
using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Parsing;

/// <summary>
/// Gemini 文本提取器
/// 负责从 Gemini 消息 parts 中提取纯文本内容
/// </summary>
public static class GeminiTextExtractor
{
    /// <summary>
    /// 从 Gemini 消息的 parts 数组中提取文本
    /// </summary>
    public static string ExtractTextFromParts(JsonNode? content)
    {
        if (content is not JsonObject contentObj ||
            !contentObj.TryGetPropertyValue("parts", out var partsNode) ||
            partsNode is not JsonArray parts)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part is JsonObject partObj &&
                partObj.TryGetPropertyValue("text", out var textNode) &&
                textNode is JsonValue textValue &&
                textValue.TryGetValue<string>(out var text))
            {
                sb.Append(text);
            }
        }
        return sb.ToString();
    }
}

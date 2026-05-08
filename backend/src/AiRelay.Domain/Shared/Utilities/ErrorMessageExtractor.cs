using System.Text.Json;

namespace AiRelay.Domain.Shared.Utilities;

/// <summary>
/// JSON 错误消息提取工具
/// 尝试从上游 API 错误响应 JSON 中提取人类可读的错误消息
/// 支持 { "error": { "message": "..." } } 和 { "message": "..." } 两种格式
/// </summary>
public static class ErrorMessageExtractor
{
    /// <summary>
    /// 从 JSON 响应体中提取错误消息
    /// </summary>
    /// <param name="body">上游 API 的响应体字符串</param>
    /// <returns>提取到的错误消息，解析失败时返回原始文本，空输入返回 null</returns>
    public static string? TryExtractMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var nestedMessage))
            {
                return nestedMessage.GetString();
            }

            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : body;
        }
        catch
        {
            return body;
        }
    }
}

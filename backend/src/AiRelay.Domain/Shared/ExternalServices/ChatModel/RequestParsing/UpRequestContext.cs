using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

/// <summary>
/// 上游请求上下文（网关 → 供应商）
/// 可变 class，Processor 链直接写入字段
/// </summary>
public class UpRequestContext
{
    public HttpMethod Method { get; set; } = HttpMethod.Post;

    // ── Url（UrlProcessor 填充）
    public string BaseUrl { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string GetFullUrl() => $"{BaseUrl}{RelativePath}{QueryString}";

    // ── Headers（HeaderProcessor 填充）
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Body（RequestBodyProcessor 填充）
    public JsonObject? BodyJson { get; set; }

    // ── 元数据（ModelIdMappingProcessor 填充）
    public string? MappedModelId { get; set; }
    public string? SessionId { get; set; }

    /// <summary>
    /// 获取 Body 内容（使用字节级截断避免整个DOM的超大字符串分配）
    /// </summary>
    public string GetBodyPreview(int maxLength = 2000)
    {
        if (BodyJson == null) return string.Empty;

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(BodyJson);
            if (bytes.Length <= maxLength) return Encoding.UTF8.GetString(bytes);

            return Encoding.UTF8.GetString(bytes.AsSpan(0, maxLength)) + "...[Truncated]";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 统一构建 HttpRequestMessage，提升通用逻辑复用率，并彻底摒弃将大对象序列化为分配型 String 进行发送导致的高内存开销
    /// </summary>
    public HttpRequestMessage BuildHttpRequestMessage()
    {
        var normalizedBase = BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/";
        var relativeUrl = RelativePath.TrimStart('/') + (QueryString ?? "");

        var request = new HttpRequestMessage(Method, normalizedBase + relativeUrl);
        foreach (var header in Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (BodyJson != null)
        {
            // 通过 HttpContent 内部进行 Json 0-copy 处理发送，不分配巨型 JSON 字符串
            request.Content = JsonContent.Create(BodyJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return request;
    }

    public string? GetUserAgent() =>
        Headers.TryGetValue("user-agent", out var ua) ? ua : null;
}

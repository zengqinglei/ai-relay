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

    public string? GetUserAgent() =>
        Headers.TryGetValue("user-agent", out var ua) ? ua : null;
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;

/// <summary>
/// 上游请求上下文（网关 → 供应商）
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

    // ── Body（RequestBodyProcessor / EnsureMutableBodyAsync 填充）
    public JsonObject? BodyJson { get; set; }

    // ── 元数据（ModelIdMappingProcessor 填充）
    public string? MappedModelId { get; set; }
    public string? SessionId { get; set; }

    /// <summary>
    /// 【核心状态】：请求体是否被要求修改？
    /// 默认 false (零损耗流转发)。只有 Processor 调用 EnsureMutableBodyAsync 才会触发 true。
    /// </summary>
    public bool RequiresMutation { get; private set; }

    /// <summary>
    /// 获取唯一可变的 JSON DOM。
    /// 一旦任何拦截器调用此方法，即标志着流转发退化为 DOM 序列化发送模式。
    /// </summary>
    public async ValueTask<JsonObject> EnsureMutableBodyAsync(DownRequestContext down)
    {
        if (BodyJson != null) return BodyJson;

        RequiresMutation = true;

        if (down.RawStream == null || down.RawStream == Stream.Null || !down.RawStream.CanRead)
        {
            BodyJson = [];
            return BodyJson;
        }

        if (down.RawStream.CanSeek)
        {
            down.RawStream.Position = 0;
        }

        try
        {
            var node = await JsonNode.ParseAsync(down.RawStream);
            BodyJson = node as JsonObject ?? [];
        }
        catch (JsonException)
        {
            BodyJson = [];
        }

        return BodyJson;
    }

    /// <summary>
    /// 获取 Body 内容预览
    /// </summary>
    public string GetBodyPreview(string? fallbackPreview, int maxLength = 2000)
    {
        if (BodyJson == null) return fallbackPreview ?? string.Empty;

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(BodyJson);
            if (bytes.Length <= maxLength) return Encoding.UTF8.GetString(bytes);

            return Encoding.UTF8.GetString(bytes.AsSpan(0, maxLength)) + "...";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 构建 HttpRequestMessage，支持双轨制发送（流转发 / DOM发送）
    /// </summary>
    public HttpRequestMessage BuildHttpRequestMessage(DownRequestContext down)
    {
        var normalizedBase = BaseUrl.TrimEnd('/');
        var relativeUrl = RelativePath.TrimStart('/') + (QueryString ?? "");

        var request = new HttpRequestMessage(Method, normalizedBase + "/" + relativeUrl);
        foreach (var header in Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // 双轨制决定逻辑
        if (RequiresMutation && BodyJson != null)
        {
            // 退化轨：DOM 序列化（零分配流式写出）
            request.Content = JsonContent.Create(BodyJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        else if (down.RawStream != null && down.RawStream != Stream.Null)
        {
            // 快速轨：网络直通流转发（重置流位置）
            // 使用 LeaveOpenStream 包装，防止 StreamContent.Dispose 关闭底层流（Fallback 重试需要重读）
            if (down.RawStream.CanSeek)
            {
                down.RawStream.Position = 0;
            }
            request.Content = new StreamContent(new LeaveOpenStream(down.RawStream));
            
            // 复制原 Content-Type
            if (down.Headers.TryGetValue("content-type", out var contentType))
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
            else
            {
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            // 复制原 Content-Length（防止 StreamContent 降级为 Chunked 导致上游解析失败）
            if (down.Headers.TryGetValue("content-length", out var clStr) && long.TryParse(clStr, out var cl))
            {
                request.Content.Headers.ContentLength = cl;
            }
        }

        return request;
    }

    public string? GetUserAgent() =>
        Headers.TryGetValue("user-agent", out var ua) ? ua : null;
}

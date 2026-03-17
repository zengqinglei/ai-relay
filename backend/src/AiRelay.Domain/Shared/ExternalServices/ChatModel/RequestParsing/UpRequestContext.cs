namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

/// <summary>
/// 上游请求上下文（网关 → 供应商）
/// 封装纯技术信息，不包含业务属性
/// </summary>
public record UpRequestContext
{
    // HTTP Method
    public required HttpMethod Method { get; init; }

    // 目标信息
    public required string BaseUrl { get; init; }
    public required string RelativePath { get; init; }
    public string? QueryString { get; init; }

    public string GetFullUrl() => $"{BaseUrl}{RelativePath}{QueryString}";

    // 请求头（转换后）
    public required Dictionary<string, string> Headers { get; init; }

    // 请求体（转换后）
    public string? BodyContent { get; set; }
    public HttpContent? HttpContent { get; init; }

    // 协议转换结果
    public string? MappedModelId { get; init; }
    public string? SessionId { get; init; }

    // 辅助方法
    public string? GetUserAgent() => Headers.TryGetValue("user-agent", out var ua) ? ua : null;
}

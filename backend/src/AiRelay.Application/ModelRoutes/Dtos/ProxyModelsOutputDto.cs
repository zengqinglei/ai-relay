using System.Text.Json.Serialization;

namespace AiRelay.Application.ModelRoutes.Dtos;

/// <summary>
/// /v1/models 代理端点响应 DTO（OpenAI 格式与 Anthropic 格式共用同一容器）。
/// 字段名通过 JsonPropertyName 固定为各协议要求的 snake_case，
/// 不受 MVC 全局 camelCase 命名策略影响；null 字段序列化时被忽略。
/// </summary>
public record ProxyModelsOutputDto
{
    /// <summary>
    /// 对象类型，OpenAI 协议固定为 "list"，Anthropic 协议为 null
    /// </summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    /// 模型条目列表
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<ProxyModelItemDto> Data { get; init; } = [];

    /// <summary>
    /// 是否还有更多条目（Anthropic 协议使用，恒为 false，OpenAI 协议为 null）
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool? HasMore { get; init; }

    /// <summary>
    /// 列表第一项的模型 ID（Anthropic 协议使用）
    /// </summary>
    [JsonPropertyName("first_id")]
    public string? FirstId { get; init; }

    /// <summary>
    /// 列表最后一项的模型 ID（Anthropic 协议使用）
    /// </summary>
    [JsonPropertyName("last_id")]
    public string? LastId { get; init; }
}

/// <summary>
/// 单个模型条目
/// </summary>
public record ProxyModelItemDto
{
    /// <summary>模型 ID</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 条目类型：Anthropic 协议为 "model"，OpenAI 协议为 null
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// 对象类型：OpenAI 协议为 "model"，Anthropic 协议为 null
    /// </summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    /// 创建时间戳（Unix 秒），OpenAI 协议使用
    /// </summary>
    [JsonPropertyName("created")]
    public long? Created { get; init; }

    /// <summary>
    /// 创建时间（RFC 3339），Anthropic 协议使用
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// 归属主体（OpenAI 协议为 "system"，Anthropic 协议为 null）
    /// </summary>
    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; init; }

    /// <summary>
    /// Anthropic 协议使用的显示名称
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }
}

namespace AiRelay.Application.ModelRoutes.Dtos;

/// <summary>
/// /v1/models 代理端点响应 DTO（OpenAI 格式，与 Anthropic 格式共用同一容器）
/// </summary>
public class ProxyModelsOutputDto
{
    /// <summary>
    /// 对象类型，OpenAI 协议固定为 "list"，Anthropic 协议为 null
    /// </summary>
    public string? Object { get; set; }

    /// <summary>
    /// 模型条目列表
    /// </summary>
    public IReadOnlyList<ProxyModelItemDto> Data { get; set; } = [];
}

/// <summary>
/// 单个模型条目
/// </summary>
public class ProxyModelItemDto
{
    /// <summary>模型 ID</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 对象类型：OpenAI 协议为 "model"，Anthropic 协议为 null
    /// </summary>
    public string? Object { get; set; }

    /// <summary>
    /// 创建时间戳（Unix 秒），OpenAI 协议使用
    /// </summary>
    public long? Created { get; set; }

    /// <summary>
    /// 归属主体（OpenAI 协议为 "system"，Anthropic 协议为 null）
    /// </summary>
    public string? OwnedBy { get; set; }

    /// <summary>
    /// Anthropic 协议使用的显示名称
    /// </summary>
    public string? DisplayName { get; set; }
}

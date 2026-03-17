using System.Text.Json.Nodes;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

/// <summary>
/// 转换后的请求上下文（协议转换的输出）
/// </summary>
public class TransformedRequestContext
{
    // ========== 转换后的数据 ==========
    public JsonObject? BodyJson { get; init; }

    // ========== 映射后的模型ID ==========
    public string? MappedModelId { get; init; }

    // ========== 协议特定 Headers（如 Antigravity 的 anthropic-* headers） ==========
    public Dictionary<string, string> ProtocolHeaders { get; init; } = new();
}

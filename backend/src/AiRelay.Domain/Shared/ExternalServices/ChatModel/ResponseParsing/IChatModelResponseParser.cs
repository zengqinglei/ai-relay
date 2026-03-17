namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

/// <summary>
/// 聊天模型响应解析器接口
/// </summary>
public interface IChatModelResponseParser
{
    /// <summary>
    /// 解析响应块（流式或非流式）
    /// </summary>
    /// <param name="chunk">数据块（通常是一行 SSE 数据或 JSON 片段）</param>
    /// <returns>解析后的结果部分</returns>
    ChatResponsePart? ParseChunk(string chunk);

    /// <summary>
    /// 解析完整的响应体（非流式）
    /// </summary>
    /// <param name="responseBody">完整响应内容</param>
    /// <returns>解析后的结果</returns>
    ChatResponsePart ParseCompleteResponse(string responseBody);
}

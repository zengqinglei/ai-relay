namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

/// <summary>
/// 请求转换器接口：负责纯协议层转换（提取模型信息、构建上游请求）
/// 两种场景均使用：零拷贝代理（SmartReverseProxy）和模型测试（DebugModel）
/// </summary>
public interface IRequestTransformer
{
    /// <summary>
    /// 阶段1: 从下游请求中提取元信息（ModelId、SessionHash），填充到 downContext
    /// </summary>
    void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId);

    /// <summary>
    /// 阶段2: 协议转换（模型映射、格式转换）
    /// 职责：将下游协议转换为上游协议，所有场景都需要
    /// 返回：转换后的请求上下文（不直接构建 HTTP 请求）
    /// </summary>
    Task<TransformedRequestContext> TransformProtocolAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 阶段4: 构建上游 HTTP 请求
    /// 职责：将转换后的上下文构建为最终的 HTTP 请求
    /// </summary>
    Task<UpRequestContext> BuildHttpRequestAsync(
        DownRequestContext downContext,
        TransformedRequestContext transformedContext,
        CancellationToken cancellationToken = default);
}

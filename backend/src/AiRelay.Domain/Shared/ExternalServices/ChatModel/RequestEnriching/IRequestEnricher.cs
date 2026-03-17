using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestEnriching;

/// <summary>
/// 代理专属请求增强接口：降级签名处理、JsonSchema 清洗、业务提示词注入等
/// 仅在零拷贝代理场景（SmartReverseProxy）调用；DebugModel 测试场景跳过此步骤
/// </summary>
public interface IRequestEnricher
{
    /// <summary>
    /// 阶段3: 代理增强（可选，仅代理场景调用）
    /// 职责：应用代理专属的增强逻辑（降级、签名、过滤等）
    /// </summary>
    void ApplyProxyEnhancements(DownRequestContext downContext, TransformedRequestContext transformedContext);
}

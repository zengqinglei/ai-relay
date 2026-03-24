using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// 面向业务的 ChatModel 处理器接口
/// 不再聚合继承子接口，所有方法直接定义于此
/// ChatModelConnectionOptions 通过构造函数注入，Handler 实例为 Transient（单次请求作用域）
/// </summary>
public interface IChatModelHandler : IResponseParser
{
    /// <summary>
    /// 是否支持平台
    /// </summary>
    bool Supports(ProviderPlatform platform);

    /// <summary>
    /// 元数据提取（由 Middleware 在 DownstreamRequestProcessor 之后单独调用）
    /// </summary>
    void ExtractModelInfo(DownRequestContext down, Guid apiKeyId);

    /// <summary>
    /// 通过 Processor 链将 DownRequestContext 转换为 UpRequestContext
    /// degradationLevel 由重试状态机维护，作为显式参数传入
    /// </summary>
    Task<UpRequestContext> ProcessRequestContextAsync(
        DownRequestContext down,
        int degradationLevel = 0,
        CancellationToken ct = default);

    /// <summary>
    /// 执行 HTTP 请求（含 Fallback BaseUrl 重试逻辑）
    /// </summary>
    Task<HttpResponseMessage> ProxyRequestAsync(
        UpRequestContext up,
        CancellationToken ct = default);

    /// <summary>
    /// 返回 SSE 行回调（用于在流式转发时提取平台特有信息，如 thoughtSignature）
    /// 默认返回 null（无需处理）
    /// </summary>
    Action<string>? GetSseLineCallback(string? sessionId);

    /// <summary>
    /// 响应异常分析
    /// </summary>
    Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody);

    /// <summary>
    /// 创建测试上下文
    /// </summary>
    DownRequestContext CreateDebugDownContext(string modelId, string message);

    /// <summary>
    /// 验证连接有效性
    /// </summary>
    Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取可用额度
    /// </summary>
    Task<IReadOnlyList<AccountQuotaInfo>?> FetchQuotaAsync(CancellationToken ct = default);

    /// <summary>
    /// 从上游 API 拉取可用模型列表
    /// </summary>
    /// <returns>
    /// 成功：返回模型列表（上游优先）
    /// 不支持/失败：返回 null（降级到静态列表）
    /// </returns>
    Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default);
}

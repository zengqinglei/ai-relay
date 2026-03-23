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
    bool Supports(ProviderPlatform platform);

    // ── 元数据提取（由 Middleware 在 DownstreamRequestProcessor 之后单独调用）
    void ExtractModelInfo(DownRequestContext down, Guid apiKeyId);

    // ── 核心请求处理
    /// <summary>
    /// 通过 Processor 链将 DownRequestContext 转换为 UpRequestContext
    /// degradationLevel 由重试状态机维护，作为显式参数传入
    /// </summary>
    Task<UpRequestContext> ProcessRequestContextAsync(
        DownRequestContext down,
        int degradationLevel = 0,
        CancellationToken ct = default);

    /// <summary>执行 HTTP 请求（含 Fallback BaseUrl 重试逻辑）</summary>
    Task<HttpResponseMessage> ProxyRequestAsync(
        UpRequestContext up,
        CancellationToken ct = default);

    /// <summary>
    /// 返回 SSE 行回调（用于在流式转发时提取平台特有信息，如 thoughtSignature）
    /// 默认返回 null（无需处理）
    /// </summary>
    Action<string>? GetSseLineCallback(string? sessionId);

    Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody);

    // ── 测试入口专用
    DownRequestContext CreateDebugDownContext(string modelId, string message);

    // ── 账号管理
    Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default);
    Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken ct = default);
}

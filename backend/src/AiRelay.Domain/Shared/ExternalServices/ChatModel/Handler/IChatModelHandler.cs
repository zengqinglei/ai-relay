using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// 面向业务的 ChatModel 处理器接口
/// </summary>
public interface IChatModelHandler
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
    /// 两阶段发送：
    /// Phase 1 - 发送请求并等待响应头，立即可读 IsSuccess / StatusCode / Headers / ErrorBody；
    /// Phase 2 - 枚举 ProxyResponse.Events 消费响应体（含 SSE 解析、ForwardBytes、UsageStats）。
    /// </summary>
    Task<ProxyResponse> SendAsync(
        UpRequestContext up,
        DownRequestContext down,
        bool isStreaming,
        CancellationToken ct = default);

    /// <summary>
    /// 检查失败响应的重试策略（是否可重试、等待时间、是否需要降级）
    /// </summary>
    Task<ModelErrorAnalysisResult> CheckRetryPolicyAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string? responseBody);

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
    Task<IReadOnlyList<ModelOption>?> GetModelsAsync(CancellationToken ct = default);
}

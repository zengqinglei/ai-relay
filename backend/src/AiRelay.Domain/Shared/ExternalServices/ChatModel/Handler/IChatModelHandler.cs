using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestEnriching;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// 聊天模型客户端接口（聚合所有能力接口）
/// </summary>
public interface IChatModelHandler
    : IRequestTransformer,
      IRequestEnricher,
      IResponseParser,
      IErrorAnalyzer,
      IConnectionValidator
{
    /// <summary>
    /// 判断是否支持指定的平台
    /// </summary>
    bool Supports(ProviderPlatform platform);

    /// <summary>
    /// 获取平台 API 地址
    /// </summary>
    string GetBaseUrl();

    /// <summary>
    /// 获取备用 API 地址（状态码满足条件时），不支持则返回 null
    /// </summary>
    string? GetFallbackBaseUrl(int statusCode);

    /// <summary>
    /// 配置客户端（凭证、BaseUrl、额外属性）
    /// </summary>
    void Configure(ChatModelConnectionOptions options);

    /// <summary>
    /// 执行 HTTP 请求（含备用端点重试）
    /// </summary>
    Task<HttpResponseMessage> ExecuteHttpRequestAsync(
        UpRequestContext upContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 【测试/管理端专用】创建调试用的标准请求上下文
    /// </summary>
    DownRequestContext CreateDebugDownContext(string modelId, string message);
}

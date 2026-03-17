using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// 错误分析器接口：将 HTTP 错误响应解析为标准化的重试决策信息
/// 仅在零拷贝代理场景（SmartReverseProxy）使用
/// </summary>
public interface IErrorAnalyzer
{
    /// <summary>
    /// 分析错误响应，返回标准化错误特征（类型、是否可重试、建议等待时间）
    /// </summary>
    Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody);
}

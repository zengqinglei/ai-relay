using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// 代理错误响应记录
/// </summary>
public record ProxyErrorResponse(int StatusCode, string ContentType, string Payload);

/// <summary>
/// 代理错误格式化器契约
/// </summary>
public interface IProxyErrorFormatter
{
    bool Supports(RouteProfile profile);

    /// <summary>
    /// 将内部异常格式化为指定平台的 HTTP 响应载荷
    /// </summary>
    /// <param name="exception">最初始拦截到的内部异常（NotFound / ServiceUnavailable / BadRequest 等）</param>
    /// <returns>包含状态码、ContentType及序列化后Payload的对象</returns>
    ProxyErrorResponse Format(Exception exception);

    /// <summary>
    /// 将上游返回的原始错误载荷规范化为指定平台的标准格式，并注入 Fallback 诱导信息
    /// </summary>
    /// <param name="statusCode">上游返回的 HTTP 状态码</param>
    /// <param name="upstreamBody">上游返回的原始响应体</param>
    /// <returns>规范化后的响应对象</returns>
    ProxyErrorResponse Normalize(int statusCode, string? upstreamBody);
}

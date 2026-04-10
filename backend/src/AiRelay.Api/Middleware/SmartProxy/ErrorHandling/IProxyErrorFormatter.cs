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
}

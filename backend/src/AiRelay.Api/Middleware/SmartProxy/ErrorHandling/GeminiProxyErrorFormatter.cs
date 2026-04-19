using System.Net.Mime;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// Gemini 格式代理错误格式化器
/// 用于适配 gemini-cli sdk
/// </summary>
public class GeminiProxyErrorFormatter : BaseProxyErrorFormatter
{
    public override bool Supports(RouteProfile profile) => profile is RouteProfile.GeminiBeta or RouteProfile.GeminiInternal;

    /// <summary>
    /// 将 HTTP 状态码映射为 Google RPC 状态字符串
    /// 参考：https://cloud.google.com/apis/design/errors#http_mapping
    /// </summary>
    private static string GetGoogleRpcStatus(int statusCode) => statusCode switch
    {
        400 => "INVALID_ARGUMENT",
        401 => "UNAUTHENTICATED",
        403 => "PERMISSION_DENIED",
        404 => "NOT_FOUND",
        429 => "RESOURCE_EXHAUSTED",
        499 => "CANCELLED",
        500 => "INTERNAL",
        501 => "NOT_IMPLEMENTED",
        503 => "UNAVAILABLE",
        504 => "DEADLINE_EXCEEDED",
        _ => "UNKNOWN"
    };

    protected override ProxyErrorResponse BuildResponse(int statusCode, string message)
    {
        var responseObj = new
        {
            error = new
            {
                code = statusCode,
                message,
                status = GetGoogleRpcStatus(statusCode)
            }
        };

        return new ProxyErrorResponse(
            statusCode,
            MediaTypeNames.Application.Json,
            JsonSerializer.Serialize(responseObj));
    }
}

using System.Net.Mime;
using System.Text.Json;
using AiRelay.Application.ModelRoutes.Handlers;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// Gemini 格式代理错误格式化器
/// 用于适配 gemini-cli sdk
/// </summary>
public class GeminiProxyErrorFormatter(RouteTerminalErrorFormatter routeTerminalErrorFormatter) : BaseProxyErrorFormatter(routeTerminalErrorFormatter)
{
    public override bool Supports(RouteProfile profile) => profile is RouteProfile.GeminiBeta or RouteProfile.GeminiInternal;

    protected override ProxyErrorResponse BuildResponse(int statusCode, string message)
    {
        var responseObj = new
        {
            error = new
            {
                code = statusCode, // 保持为数字，与 Google SDK 官方标准一致
                message,           // 包含前置 Normalize 注入的 (Overloaded) 关键字
                status = GetGoogleRpcStatus(statusCode)
            }
        };

        return new ProxyErrorResponse(
            statusCode,
            MediaTypeNames.Application.Json,
            JsonSerializer.Serialize(responseObj));
    }

    /// <summary>
    /// 将 HTTP 状态码映射为 Google RPC 状态字符串
    /// </summary>
    private static string GetGoogleRpcStatus(int statusCode) => statusCode switch
    {
        400 => "INVALID_ARGUMENT",
        401 => "UNAUTHENTICATED",
        403 => "PERMISSION_DENIED",
        404 => "NOT_FOUND",
        413 => "OUT_OF_RANGE",
        429 => "RESOURCE_EXHAUSTED",
        499 => "CANCELLED",
        500 => "INTERNAL",
        501 => "NOT_IMPLEMENTED",
        503 => "UNAVAILABLE",
        504 => "DEADLINE_EXCEEDED",
        _ => "UNKNOWN"
    };
}

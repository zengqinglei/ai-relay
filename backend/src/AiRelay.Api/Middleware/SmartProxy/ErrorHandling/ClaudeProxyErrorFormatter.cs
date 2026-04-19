using System.Net.Mime;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// Claude 格式代理错误格式化器
/// 用于适配 claude-cli sdk
/// </summary>
public class ClaudeProxyErrorFormatter : BaseProxyErrorFormatter
{
    public override bool Supports(RouteProfile profile) => profile is RouteProfile.ClaudeMessages;

    /// <summary>
    /// 将 HTTP 状态码映射为 Anthropic 标准错误类型字符串
    /// 参考：https://docs.anthropic.com/en/api/errors
    /// </summary>
    private static string GetClaudeErrorType(int statusCode) => statusCode switch
    {
        400 => "invalid_request_error",
        401 => "authentication_error",
        402 => "billing_error",
        403 => "permission_error",
        404 => "not_found_error",
        413 => "request_too_large",
        429 => "rate_limit_error",
        500 => "api_error",
        503 or 529 => "overloaded_error",
        504 => "timeout_error",
        _ => "api_error"
    };

    protected override ProxyErrorResponse BuildResponse(int statusCode, string message)
    {
        var responseObj = new
        {
            type = "error",
            error = new
            {
                type = GetClaudeErrorType(statusCode),
                message
            }
        };

        return new ProxyErrorResponse(
            statusCode,
            MediaTypeNames.Application.Json,
            JsonSerializer.Serialize(responseObj));
    }
}

using System.Net.Mime;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// OpenAI 格式代理错误格式化器
/// 多数第三方 SDK 能兼容这种格式的解析
/// </summary>
public class OpenAIProxyErrorFormatter : BaseProxyErrorFormatter
{
    public override bool Supports(RouteProfile profile) => profile is RouteProfile.ChatCompletions or RouteProfile.OpenAiResponses or RouteProfile.OpenAiCodex;

    /// <summary>
    /// 将 HTTP 状态码映射为 OpenAI 标准错误类型字符串
    /// 参考：https://platform.openai.com/docs/guides/error-codes
    /// </summary>
    private static string GetOpenAiErrorType(int statusCode) => statusCode switch
    {
        400 => "invalid_request_error",
        401 => "authentication_error",
        403 => "permission_denied",
        404 => "not_found",
        429 => "rate_limit_exceeded",
        _ => "server_error"
    };

    protected override ProxyErrorResponse BuildResponse(int statusCode, string message)
    {
        var responseObj = new
        {
            error = new
            {
                message,
                type = GetOpenAiErrorType(statusCode),
                param = (string?)null,
                code = "gateway_error"
            }
        };

        return new ProxyErrorResponse(
            statusCode,
            MediaTypeNames.Application.Json,
            JsonSerializer.Serialize(responseObj));
    }
}

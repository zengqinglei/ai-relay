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

    protected override ProxyErrorResponse BuildResponse(int statusCode, string message)
    {
        var responseObj = new
        {
            error = new
            {
                message,
                type = "proxy_failure",
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

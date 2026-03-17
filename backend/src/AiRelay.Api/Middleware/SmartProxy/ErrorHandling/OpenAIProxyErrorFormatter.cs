using System.Net.Mime;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// OpenAI 格式代理错误格式化器
/// 多数第三方 SDK 能兼容这种格式的解析
/// </summary>
public record OpenAIProxyErrorFormatter : IProxyErrorFormatter
{
    public ProviderPlatform Platform => ProviderPlatform.OPENAI_OAUTH;

    public ProxyErrorResponse Format(Exception exception)
    {
        var responseObj = new
        {
            error = new
            {
                message = $"AiRelay Gateway Error: {exception.Message}",
                type = "proxy_failure",
                param = (string?)null,
                code = "gateway_error"
            }
        };

        return new ProxyErrorResponse(
            StatusCodes.Status400BadRequest,
            MediaTypeNames.Application.Json,
            JsonSerializer.Serialize(responseObj));
    }
}

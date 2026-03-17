using System.Net.Mime;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// Gemini 格式代理错误格式化器
/// 用于适配 gemini-cli sdk
/// </summary>
public record GeminiProxyErrorFormatter : IProxyErrorFormatter
{
    public ProviderPlatform Platform => ProviderPlatform.GEMINI_OAUTH;

    public ProxyErrorResponse Format(Exception exception)
    {
        var responseObj = new
        {
            error = new
            {
                code = 400,
                message = $"AiRelay Gateway Error: {exception.Message}",
                status = "FAILED_PRECONDITION"
            }
        };

        return new ProxyErrorResponse(
            StatusCodes.Status400BadRequest,
            MediaTypeNames.Application.Json,
            JsonSerializer.Serialize(responseObj));
    }
}

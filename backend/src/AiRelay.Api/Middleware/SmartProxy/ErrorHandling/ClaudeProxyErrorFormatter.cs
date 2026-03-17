using System.Net.Mime;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// Claude 格式代理错误格式化器
/// 用于适配 claude-cli sdk
/// </summary>
public record ClaudeProxyErrorFormatter : IProxyErrorFormatter
{
    public ProviderPlatform Platform => ProviderPlatform.CLAUDE_OAUTH;

    public ProxyErrorResponse Format(Exception exception)
    {
        var responseObj = new
        {
            type = "error",
            error = new
            {
                type = "invalid_request_error",
                message = $"AiRelay Gateway Error: {exception.Message}"
            }
        };

        return new ProxyErrorResponse(
            StatusCodes.Status400BadRequest,
            MediaTypeNames.Application.Json,
            JsonSerializer.Serialize(responseObj));
    }
}

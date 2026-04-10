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

    protected override ProxyErrorResponse BuildResponse(int statusCode, string message)
    {
        var responseObj = new
        {
            type = "error",
            error = new
            {
                type = "invalid_request_error",
                message
            }
        };

        return new ProxyErrorResponse(
            statusCode,
            MediaTypeNames.Application.Json,
            JsonSerializer.Serialize(responseObj));
    }
}

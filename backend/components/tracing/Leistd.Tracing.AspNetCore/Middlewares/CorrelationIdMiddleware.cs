using Leistd.Tracing.Core.Constants;
using Leistd.Tracing.Core.Options;
using Leistd.Tracing.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Leistd.Tracing.AspNetCore.Middlewares;

public class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger,
    IOptions<CorrelationIdOptions> options)
{
    private readonly CorrelationIdOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        if (!_options.Enable)
        {
            await next(context);
            return;
        }

        var correlationId = GetCorrelationIdFromRequest(context, correlationIdProvider);

        // 1. 设置 Provider 上下文 (用于程序内传递)
        using (correlationIdProvider.Change(correlationId))
        {
            // 2. 设置日志上下文 (用于 Serilog 等日志库)
            using (logger.BeginScope(new Dictionary<string, object>
            {
                { CorrelationIdConstants.TraceIdLogKey, correlationId }
            }))
            {
                // 3. 设置响应头 (如果需要)
                if (_options.SetResponseHeader)
                {
                    CheckAndSetCorrelationIdOnResponse(context, correlationId);
                }

                await next(context);
            }
        }
    }

    private string GetCorrelationIdFromRequest(HttpContext context, ICorrelationIdProvider provider)
    {
        var headers = _options.GetHttpHeaderNames();
        foreach (var headerName in headers)
        {
            if (context.Request.Headers.TryGetValue(headerName, out StringValues headerValue))
            {
                var correlationId = headerValue.ToString();
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    return correlationId;
                }
            }
        }

        // 否则生成新 ID
        var newId = provider.Create();
        return newId;
    }

    private void CheckAndSetCorrelationIdOnResponse(HttpContext context, string correlationId)
    {
        context.Response.OnStarting(() =>
        {
            var headers = _options.GetHttpHeaderNames();
            foreach (var headerName in headers)
            {
                if (!context.Response.Headers.ContainsKey(headerName))
                {
                    context.Response.Headers.Append(headerName, correlationId);
                }
            }
            return Task.CompletedTask;
        });
    }
}
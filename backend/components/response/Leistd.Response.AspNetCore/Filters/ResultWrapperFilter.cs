using Leistd.Response.Core.Wrapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Leistd.Response.AspNetCore.Filters;

/// <summary>
/// 统一成功响应包装过滤器
/// </summary>
public class ResultWrapperFilter(ILogger<ResultWrapperFilter> logger) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.Any(m => m is Attributes.NoWrapAttribute))
        {
            await next();
            return;
        }

        if (context.Result is ObjectResult objectResult && objectResult.Value is not Result)
        {
            if (objectResult.StatusCode is null or >= 200 and < 300)
            {
                logger.LogDebug("包装响应: {ActionName}", context.ActionDescriptor.DisplayName);

                var wrappedResult = Result<object?>.Ok(objectResult.Value);

                context.Result = new ObjectResult(wrappedResult)
                {
                    StatusCode = objectResult.StatusCode ?? 200
                };
            }
        }

        await next();
    }
}

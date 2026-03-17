using Castle.DynamicProxy;
using Leistd.DynamicProxy;
using Leistd.Tracing.Core.Constants;
using Leistd.Tracing.Core.Services;
using Microsoft.Extensions.Logging;

namespace Leistd.Tracing.Core.Interceptors;

public class CorrelationIdInterceptor(
    ICorrelationIdProvider correlationIdProvider,
    ILogger<CorrelationIdInterceptor> logger) : BaseAsyncInterceptor
{
    /// <summary>
    /// 拦截器优先级：最高 (最外层)
    /// 确保在 UnitOfWork 等其他拦截器之前执行，以便日志上下文覆盖整个链路
    /// </summary>
    public override int Order => -1000;

    protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    {
        await ExecuteInScope<object?>(invocation, async () =>
        {
            await proceed(invocation, proceedInfo);
            return null;
        });
    }

    protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    {
        return await ExecuteInScope(invocation, async () => await proceed(invocation, proceedInfo));
    }

    private async Task<T> ExecuteInScope<T>(IInvocation invocation, Func<Task<T>> proceed)
    {
        // 逻辑：如果当前上下文中已经有 ID，则不做任何操作
        var currentId = correlationIdProvider.Get();
        if (!string.IsNullOrEmpty(currentId))
        {
            return await proceed();
        }

        var newId = correlationIdProvider.Create();
        using (correlationIdProvider.Change(newId))
        {
            // 同时开启日志 Scope
            using (logger.BeginScope(new Dictionary<string, object>
            {
                { CorrelationIdConstants.TraceIdLogKey, newId }
            }))
            {
                logger.LogDebug("TraceId 上下文(AOP)已初始化: {TraceId} [Method: {Method}]", newId, invocation.Method.Name);
                return await proceed();
            }
        }
    }
}
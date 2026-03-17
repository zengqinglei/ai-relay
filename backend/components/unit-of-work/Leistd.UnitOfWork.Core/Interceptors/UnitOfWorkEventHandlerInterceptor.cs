using System.Reflection;
using Castle.DynamicProxy;
using Leistd.DynamicProxy;
using Leistd.EventBus.Core.Event;
using Leistd.EventBus.Core.EventHandler;
using Leistd.UnitOfWork.Core.Events;
using Microsoft.Extensions.Logging;

namespace Leistd.UnitOfWork.Core.Interceptor;

/// <summary>
/// 工作单元事件处理器拦截器 - 根据阶段决定是否执行处理器
/// </summary>
public class UnitOfWorkEventHandlerInterceptor(ILogger<UnitOfWorkEventHandlerInterceptor>? logger = null)
    : BaseAsyncInterceptor
{
    protected override async Task InterceptAsync(
        IInvocation invocation,
        IInvocationProceedInfo proceedInfo,
        Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    {
        if (!ShouldExecute(invocation, out var reason))
        {
            logger?.LogDebug("跳过事件处理器 {Handler}，原因: {Reason}", invocation.TargetType?.Name, reason);
            return;
        }

        await proceed(invocation, proceedInfo);
    }

    protected override async Task<TResult> InterceptAsync<TResult>(
        IInvocation invocation,
        IInvocationProceedInfo proceedInfo,
        Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    {
        if (!ShouldExecute(invocation, out var reason))
        {
            logger?.LogDebug("跳过事件处理器 {Handler}，原因: {Reason}", invocation.TargetType?.Name, reason);
            return default!;
        }

        return await proceed(invocation, proceedInfo);
    }

    private bool ShouldExecute(IInvocation invocation, out string reason)
    {
        reason = string.Empty;

        // 只拦截 HandleAsync 方法
        if (invocation.Method.Name != nameof(IEventHandler<IEvent>.HandleAsync))
        {
            return true;
        }

        // 获取处理器类上的 [UnitOfWorkEventHandler] 特性
        var handlerType = invocation.TargetType;
        var attribute = handlerType?.GetCustomAttribute<UnitOfWorkEventHandlerAttribute>();

        // 无特性，默认允许执行
        if (attribute == null)
        {
            return true;
        }

        // 获取当前阶段，无阶段时默认为 AfterCommit
        var currentPhase = UnitOfWorkContext.CurrentPhase ?? UnitOfWorkPhase.AfterCommit;

        // 阶段匹配才执行
        if (currentPhase == attribute.Phase)
        {
            return true;
        }

        reason = $"当前阶段 {currentPhase}，处理器配置阶段 {attribute.Phase}";
        return false;
    }
}

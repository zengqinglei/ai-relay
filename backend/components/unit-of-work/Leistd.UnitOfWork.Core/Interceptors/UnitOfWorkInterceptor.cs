using System.Reflection;
using Castle.DynamicProxy;
using Leistd.DynamicProxy;
using Leistd.UnitOfWork.Core.Attributes;
using Leistd.UnitOfWork.Core.Options;
using Leistd.UnitOfWork.Core.Uow;
using Microsoft.Extensions.Logging;

namespace Leistd.UnitOfWork.Core.Interceptor;

/// <summary>
/// 工作单元拦截器（支持同步和异步方法）
/// </summary>
public class UnitOfWorkInterceptor : BaseAsyncInterceptor
{
    private readonly UnitOfWorkOptions _unitOfWorkOptions;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<UnitOfWorkInterceptor>? _logger;

    public UnitOfWorkInterceptor(
        UnitOfWorkOptions unitOfWorkOptions,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<UnitOfWorkInterceptor>? logger = null)
    {
        _unitOfWorkOptions = unitOfWorkOptions;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    protected override async Task InterceptAsync(
        IInvocation invocation,
        IInvocationProceedInfo proceedInfo,
        Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    {
        var method = GetMethodInfo(invocation);
        var unitOfWorkOptions = GetUnitOfWorkAttribute(method);

        if (unitOfWorkOptions == null)
        {
            await proceed(invocation, proceedInfo);
            return;
        }

        _logger?.LogDebug("拦截方法 {Method}，开始工作单元", method.Name);

        var uow = await _unitOfWorkManager.BeginAsync(unitOfWorkOptions, requiresNew: false);

        try
        {
            await proceed(invocation, proceedInfo);
            await uow.CompleteAsync();

            _logger?.LogDebug("方法 {Method} 执行完成，工作单元已提交", method.Name);
        }
        catch
        {
            uow.Dispose();
            throw;
        }
    }

    protected override async Task<TResult> InterceptAsync<TResult>(
        IInvocation invocation,
        IInvocationProceedInfo proceedInfo,
        Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    {
        var method = GetMethodInfo(invocation);
        var unitOfWorkOptions = GetUnitOfWorkAttribute(method);

        if (unitOfWorkOptions == null)
        {
            return await proceed(invocation, proceedInfo);
        }

        _logger?.LogDebug("拦截方法 {Method}，开始工作单元", method.Name);

        var uow = await _unitOfWorkManager.BeginAsync(unitOfWorkOptions, requiresNew: false);

        try
        {
            var result = await proceed(invocation, proceedInfo);
            await uow.CompleteAsync();

            _logger?.LogDebug("方法 {Method} 执行完成，工作单元已提交", method.Name);

            return result;
        }
        catch
        {
            uow.Dispose();
            throw;
        }
    }

    private MethodInfo GetMethodInfo(IInvocation invocation)
    {
        return invocation.MethodInvocationTarget ?? invocation.GetConcreteMethod();
    }

    /// <summary>
    /// 获取方法或类上的 UnitOfWork 配置
    /// </summary>
    private UnitOfWorkOptions? GetUnitOfWorkAttribute(MethodInfo methodInfo)
    {
        // 检查方法级别特性
        var attrs = methodInfo.GetCustomAttributes(true).OfType<UnitOfWorkAttribute>().ToArray();
        if (attrs.Length > 0)
        {
            return attrs[0].CreateOptionsFromDefault(_unitOfWorkOptions);
        }

        // 检查类级别特性
        attrs = methodInfo.DeclaringType?.GetTypeInfo()
            .GetCustomAttributes(true).OfType<UnitOfWorkAttribute>().ToArray()
            ?? Array.Empty<UnitOfWorkAttribute>();

        if (attrs.Length > 0)
        {
            return attrs[0].CreateOptionsFromDefault(_unitOfWorkOptions);
        }

        // 仅支持显式 [UnitOfWork] 特性
        return null;
    }
}

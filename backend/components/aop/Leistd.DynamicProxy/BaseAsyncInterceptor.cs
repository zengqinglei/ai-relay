using Castle.DynamicProxy;

namespace Leistd.DynamicProxy;

/// <summary>
/// Base class for async interceptors (supports both sync and async methods)
/// </summary>
public abstract class BaseAsyncInterceptor : AsyncInterceptorBase
{
    /// <summary>
    /// 拦截器执行顺序（数值越小越先执行，默认 0）
    /// </summary>
    public virtual int Order => 0;
}

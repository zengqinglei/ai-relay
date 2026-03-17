namespace Leistd.Ddd.Domain.DataFilters;

/// <summary>
/// 数据过滤器接口（泛型版本）
/// </summary>
/// <typeparam name="TFilter">过滤器类型（如 ISoftDelete）</typeparam>
public interface IDataFilter<TFilter>
    where TFilter : class
{
    /// <summary>
    /// 禁用过滤器（返回一个 IDisposable，释放时恢复过滤器状态）
    /// </summary>
    /// <returns>实现了 IDisposable 的对象，用于恢复过滤器状态</returns>
    IDisposable Disable();

    /// <summary>
    /// 启用过滤器（返回一个 IDisposable，释放时恢复过滤器状态）
    /// </summary>
    /// <returns>实现了 IDisposable 的对象，用于恢复过滤器状态</returns>
    IDisposable Enable();

    /// <summary>
    /// 检查过滤器是否已启用
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// 数据过滤器接口（非泛型版本，用于动态类型操作）
/// </summary>
public interface IDataFilter
{
    /// <summary>
    /// 禁用指定类型的过滤器
    /// </summary>
    IDisposable Disable<TFilter>() where TFilter : class;

    /// <summary>
    /// 启用指定类型的过滤器
    /// </summary>
    IDisposable Enable<TFilter>() where TFilter : class;

    /// <summary>
    /// 检查指定类型的过滤器是否已启用
    /// </summary>
    bool IsEnabled<TFilter>() where TFilter : class;
}

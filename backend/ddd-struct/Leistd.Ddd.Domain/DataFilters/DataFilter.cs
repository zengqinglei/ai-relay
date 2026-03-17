using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Leistd.Ddd.Domain.DataFilters;

/// <summary>
/// 数据过滤器管理器（非泛型版本，用于动态类型操作）
/// </summary>
public class DataFilter(IServiceProvider serviceProvider) : IDataFilter
{
    private readonly ConcurrentDictionary<Type, object> _filters = new();

    public IDisposable Disable<TFilter>() where TFilter : class
    {
        return GetFilter<TFilter>().Disable();
    }

    public IDisposable Enable<TFilter>() where TFilter : class
    {
        return GetFilter<TFilter>().Enable();
    }

    public bool IsEnabled<TFilter>() where TFilter : class
    {
        return GetFilter<TFilter>().IsEnabled;
    }

    private IDataFilter<TFilter> GetFilter<TFilter>() where TFilter : class
    {
        return (_filters.GetOrAdd(
            typeof(TFilter),
            _ => serviceProvider.GetRequiredService<IDataFilter<TFilter>>()
        ) as IDataFilter<TFilter>)!;
    }
}

/// <summary>
/// 数据过滤器管理器实现（泛型版本）
/// </summary>
/// <typeparam name="TFilter">过滤器类型</typeparam>
public class DataFilter<TFilter> : IDataFilter<TFilter>
    where TFilter : class
{
    private readonly AsyncLocal<FilterState> _filterState = new();

    /// <summary>
    /// 检查过滤器是否已启用（默认启用）
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            EnsureInitialized();
            return _filterState.Value!.StateStack.Count > 0
                ? _filterState.Value.StateStack.Peek()
                : true; // 默认启用
        }
    }

    /// <summary>
    /// 禁用过滤器
    /// </summary>
    public IDisposable Disable()
    {
        return SetIsEnabled(false);
    }

    /// <summary>
    /// 启用过滤器
    /// </summary>
    public IDisposable Enable()
    {
        return SetIsEnabled(true);
    }

    /// <summary>
    /// 设置过滤器启用状态
    /// </summary>
    private IDisposable SetIsEnabled(bool isEnabled)
    {
        EnsureInitialized();

        _filterState.Value!.StateStack.Push(isEnabled);

        return new DisposeAction(() =>
        {
            if (_filterState.Value.StateStack.Count > 0)
            {
                _filterState.Value.StateStack.Pop();
            }
        });
    }

    /// <summary>
    /// 确保过滤器状态已初始化
    /// </summary>
    private void EnsureInitialized()
    {
        if (_filterState.Value == null)
        {
            _filterState.Value = new FilterState();
        }
    }

    /// <summary>
    /// 过滤器状态（使用栈支持嵌套场景）
    /// </summary>
    private class FilterState
    {
        public Stack<bool> StateStack { get; } = new();
    }

    /// <summary>
    /// 用于恢复状态的 Dispose 助手类
    /// </summary>
    private class DisposeAction(Action action) : IDisposable
    {
        public void Dispose()
        {
            action();
        }
    }
}

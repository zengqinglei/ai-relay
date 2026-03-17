using System.Security.Claims;

namespace Leistd.Security.Claims;

/// <summary>
/// 当前认证主体访问器抽象基类
/// </summary>
public abstract class CurrentPrincipalAccessor : ICurrentPrincipalAccessor
{
    private readonly AsyncLocal<ClaimsPrincipal?> _currentPrincipal = new();

    /// <inheritdoc />
    public ClaimsPrincipal? Principal
    {
        get
        {
            // 优先使用显式设置的 Principal
            var principal = _currentPrincipal.Value;
            if (principal is not null)
                return principal;

            // 回退到实际认证源（由派生类实现）
            return GetClaimsPrincipal();
        }
    }

    /// <summary>
    /// 获取实际的认证主体（由派生类实现）
    /// </summary>
    /// <returns>认证主体，如果未认证则返回 null</returns>
    /// <remarks>
    /// 派生类实现示例：
    /// - HttpContextCurrentPrincipalAccessor: 从 HttpContext.User 获取
    /// - TestCurrentPrincipalAccessor: 返回 null 或测试用户
    /// </remarks>
    protected abstract ClaimsPrincipal? GetClaimsPrincipal();

    /// <inheritdoc />
    public IDisposable Change(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var parent = _currentPrincipal.Value;
        _currentPrincipal.Value = principal;

        return new DisposeAction(() =>
        {
            _currentPrincipal.Value = parent;  // 自动恢复父上下文
        });
    }
}

/// <summary>
/// Dispose 动作包装器
/// </summary>
/// <param name="action">要执行的动作</param>
file sealed class DisposeAction(Action action) : IDisposable
{
    private Action? _action = action ?? throw new ArgumentNullException(nameof(action));

    public void Dispose()
    {
        var action = Interlocked.Exchange(ref _action, null);
        action?.Invoke();
    }
}

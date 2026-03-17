using System.Security.Claims;

namespace Leistd.Security.Claims;

/// <summary>
/// 当前认证主体访问器
/// </summary>
public interface ICurrentPrincipalAccessor
{
    /// <summary>
    /// 获取当前认证主体
    /// </summary>
    ClaimsPrincipal? Principal { get; }

    /// <summary>
    /// 临时切换认证主体（支持嵌套和自动恢复）
    /// </summary>
    /// <param name="principal">要切换到的认证主体</param>
    /// <returns>Dispose 时自动恢复到之前的认证主体</returns>
    /// <remarks>
    /// 使用场景：
    /// - 后台任务中设置系统用户
    /// - 单元测试中模拟用户
    /// - 跨用户操作
    /// </remarks>
    IDisposable Change(ClaimsPrincipal principal);
}

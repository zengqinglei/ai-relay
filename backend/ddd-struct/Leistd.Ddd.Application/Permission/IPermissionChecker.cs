using System.Security.Claims;

namespace Leistd.Ddd.Application.Permission;

/// <summary>
/// 权限检查器
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// 检查当前用户是否拥有指定权限
    /// </summary>
    /// <param name="name">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果拥有权限则返回 true</returns>
    Task<bool> IsGrantedAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查指定主体是否拥有指定权限
    /// </summary>
    /// <param name="claimsPrincipal">认证主体</param>
    /// <param name="name">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果拥有权限则返回 true</returns>
    Task<bool> IsGrantedAsync(
        ClaimsPrincipal? claimsPrincipal,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查当前用户是否拥有指定的多个权限
    /// </summary>
    /// <param name="names">权限名称数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>多权限检查结果</returns>
    Task<MultiplePermissionGrantResult> IsGrantedAsync(
        string[] names,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查指定主体是否拥有指定的多个权限
    /// </summary>
    /// <param name="claimsPrincipal">认证主体</param>
    /// <param name="names">权限名称数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>多权限检查结果</returns>
    Task<MultiplePermissionGrantResult> IsGrantedAsync(
        ClaimsPrincipal? claimsPrincipal,
        string[] names,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 多权限检查结果
/// </summary>
/// <param name="Results">权限检查结果字典（权限名 -> 是否授予）</param>
public readonly record struct MultiplePermissionGrantResult(
    IReadOnlyDictionary<string, bool> Results)
{
    /// <summary>
    /// 是否所有权限都已授予
    /// </summary>
    public bool AllGranted => Results.Values.All(x => x);

    /// <summary>
    /// 是否至少有一个权限已授予
    /// </summary>
    public bool AnyGranted => Results.Values.Any(x => x);
}

using System.Security.Claims;

namespace Leistd.Security.Users;

/// <summary>
/// 当前用户信息
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// 是否已认证
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 用户唯一标识符
    /// </summary>
    Guid? Id { get; }

    /// <summary>
    /// 用户名
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// 用户显示名称
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// 电子邮箱
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// 电话号码
    /// </summary>
    string? PhoneNumber { get; }

    /// <summary>
    /// 获取所有角色
    /// </summary>
    /// <returns>角色名称数组</returns>
    string[] GetRoles();

    /// <summary>
    /// 检查用户是否在指定角色中
    /// </summary>
    /// <param name="roleName">角色名称（不区分大小写）</param>
    /// <returns>如果用户在该角色中则返回 true</returns>
    bool IsInRole(string roleName);

    /// <summary>
    /// 查找指定类型的第一个 Claim
    /// </summary>
    /// <param name="claimType">Claim 类型</param>
    /// <returns>找到的 Claim，如果不存在则返回 null</returns>
    Claim? FindClaim(string claimType);

    /// <summary>
    /// 查找指定类型的所有 Claims
    /// </summary>
    /// <param name="claimType">Claim 类型</param>
    /// <returns>找到的 Claims 数组</returns>
    Claim[] FindClaims(string claimType);

    /// <summary>
    /// 获取所有 Claims
    /// </summary>
    /// <returns>所有 Claims 数组</returns>
    Claim[] GetAllClaims();
}

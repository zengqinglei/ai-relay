using AiRelay.Domain.Users.Constants;
using Leistd.Security.Users;

namespace AiRelay.Domain.Users.Specifications;

/// <summary>
/// 用户作用域规约：封装 Admin 角色判断与数据过滤用 UserId 解析逻辑
/// </summary>
public static class UserScopeSpecifications
{
    /// <summary>
    /// 当前用户是否为管理员
    /// </summary>
    public static bool IsAdmin(ICurrentUser currentUser)
    {
        return currentUser.IsInRole(AdminConstant.RoleName);
    }

    /// <summary>
    /// 解析数据过滤用的 UserId。
    /// 普通用户始终返回自身 Id；Admin 默认返回 null（看全量），当 onlyCurrentUser 为 true 时返回自身 Id。
    /// </summary>
    public static Guid? ResolveScopedUserId(ICurrentUser currentUser, bool? onlyCurrentUser = null)
    {
        if (!IsAdmin(currentUser))
        {
            return currentUser.Id!.Value;
        }

        return onlyCurrentUser == true ? currentUser.Id!.Value : null;
    }
}

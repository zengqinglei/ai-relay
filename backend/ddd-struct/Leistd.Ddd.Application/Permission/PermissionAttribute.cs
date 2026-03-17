using Leistd.Exception.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.Ddd.Application.Permission;

/// <summary>
/// 权限验证特性
/// 支持单个或多个权限检查
/// </summary>
/// <example>
/// [Permission("Users.View")]  // 单个权限
/// [Permission("Users.View", "Users.Create")]  // 多个权限（默认任一即可）
/// [Permission("Users.View", "Users.Create", RequireAll = true)]  // 多个权限（要求全部）
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute : AuthorizeAttribute, IAsyncAuthorizationFilter
{
    /// <summary>
    /// 所需权限名称列表
    /// </summary>
    public string[] Permissions { get; }

    /// <summary>
    /// 是否要求所有权限（true）还是满足任一权限即可（false，默认）
    /// </summary>
    public bool RequireAll { get; set; }

    /// <summary>
    /// 权限验证特性构造函数
    /// </summary>
    /// <param name="permissions">所需权限名称，支持单个或多个</param>
    public PermissionAttribute(params string[] permissions)
    {
        if (permissions == null || permissions.Length == 0)
            throw new BadRequestException($"{nameof(permissions)} 至少需要指定一个权限");

        Permissions = permissions;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // 检查是否有 AllowAnonymous 特性
        if (context.ActionDescriptor.EndpointMetadata.Any(m => m is IAllowAnonymous))
        {
            return;
        }

        // 检查用户是否已认证
        if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // 获取权限检查器服务
        var permissionChecker = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionChecker>();

        // 单个权限检查
        if (Permissions.Length == 1)
        {
            var isGranted = await permissionChecker.IsGrantedAsync(Permissions[0]);
            if (!isGranted)
            {
                context.Result = new ForbidResult();
            }
            return;
        }

        // 多个权限检查
        var result = await permissionChecker.IsGrantedAsync(Permissions);
        var isMultiGranted = RequireAll ? result.AllGranted : result.AnyGranted;

        if (!isMultiGranted)
        {
            context.Result = new ForbidResult();
        }
    }
}

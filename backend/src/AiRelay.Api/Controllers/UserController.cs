using AiRelay.Application.Permissions.Provider;
using AiRelay.Application.Users.AppServices;
using AiRelay.Application.Users.Dtos;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Application.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 用户管理控制器
/// </summary>
[Authorize]
[Route("api/v1/users")]
public class UserController(IUserAppService userAppService) : BaseController
{
    /// <summary>
    /// 获取用户列表（需要用户查看权限）
    /// </summary>
    [HttpGet]
    [Permission(PermissionConstant.Users.Default)]
    public async Task<PagedResultDto<UserManagementOutputDto>> GetPagedListAsync(
        [FromQuery] GetUserPagedInputDto input,
        CancellationToken cancellationToken)
    {
        return await userAppService.GetPagedListAsync(input, cancellationToken);
    }

    /// <summary>
    /// 获取用户详情（需要用户查看权限）
    /// </summary>
    [HttpGet("{id}")]
    [Permission(PermissionConstant.Users.Default)]
    public async Task<UserManagementOutputDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await userAppService.GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// 创建用户（需要用户创建权限）
    /// </summary>
    [HttpPost]
    [Permission(PermissionConstant.Users.Create)]
    public async Task<UserManagementOutputDto> CreateAsync(
        [FromBody] CreateUserInputDto input,
        CancellationToken cancellationToken)
    {
        return await userAppService.CreateAsync(input, cancellationToken);
    }

    /// <summary>
    /// 更新用户（需要用户更新权限）
    /// </summary>
    [HttpPut("{id}")]
    [Permission(PermissionConstant.Users.Update)]
    public async Task<UserManagementOutputDto> UpdateAsync(
        Guid id,
        [FromBody] UpdateUserInputDto input,
        CancellationToken cancellationToken)
    {
        return await userAppService.UpdateAsync(id, input, cancellationToken);
    }

    /// <summary>
    /// 启用用户（需要用户更新权限）
    /// </summary>
    [HttpPatch("{id}/enable")]
    [Permission(PermissionConstant.Users.Update)]
    public async Task EnableAsync(Guid id, CancellationToken cancellationToken)
    {
        await userAppService.EnableAsync(id, cancellationToken);
    }

    /// <summary>
    /// 禁用用户（需要用户更新权限）
    /// </summary>
    [HttpPatch("{id}/disable")]
    [Permission(PermissionConstant.Users.Update)]
    public async Task DisableAsync(Guid id, CancellationToken cancellationToken)
    {
        await userAppService.DisableAsync(id, cancellationToken);
    }

    /// <summary>
    /// 重置用户密码（需要用户更新权限）
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [Permission(PermissionConstant.Users.Update)]
    public async Task ResetPasswordAsync(
        Guid id,
        [FromBody] ResetUserPasswordInputDto input,
        CancellationToken cancellationToken)
    {
        await userAppService.ResetPasswordAsync(id, input, cancellationToken);
    }

    /// <summary>
    /// 删除用户（需要用户删除权限）
    /// </summary>
    [HttpDelete("{id}")]
    [Permission(PermissionConstant.Users.Delete)]
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await userAppService.DeleteAsync(id, cancellationToken);
    }
}

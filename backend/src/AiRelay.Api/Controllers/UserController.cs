using Leistd.Ddd.Application.Permission;
using AiRelay.Application.Auth.Dtos;
using AiRelay.Application.Users.AppServices;
using AiRelay.Application.Users.Dtos;
using Leistd.Ddd.Application.Contracts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiRelay.Application.Permissions.Provider;

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
    public async Task<PagedResultDto<UserOutputDto>> GetPagedListAsync(
        [FromQuery] GetUserPagedInputDto input,
        CancellationToken cancellationToken)
    {
        return await userAppService.GetPagedListAsync(input, cancellationToken);
    }

    /// <summary>
    /// 创建用户（需要用户创建权限）
    /// </summary>
    [HttpPost]
    [Permission(PermissionConstant.Users.Create)]
    public async Task<UserOutputDto> CreateAsync(
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
    public async Task<UserOutputDto> UpdateAsync(
        Guid id,
        [FromBody] UpdateUserInputDto input,
        CancellationToken cancellationToken)
    {
        return await userAppService.UpdateAsync(id, input, cancellationToken);
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

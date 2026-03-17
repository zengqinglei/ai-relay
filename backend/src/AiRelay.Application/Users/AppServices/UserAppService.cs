using AiRelay.Application.Auth.Dtos;
using AiRelay.Application.Users.Dtos;
using AiRelay.Domain.Auth.Entities;
using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.Users.AppServices;

/// <summary>
/// 用户应用服务
/// </summary>
public class UserAppService(
    IRepository<User, Guid> userRepository,
    IRepository<Role, Guid> roleRepository,
    IRepository<UserRole, Guid> userRoleRepository,
    IRepository<ExternalLoginConnection, Guid> externalLoginRepository,
    UserDomainService userDomainService,
    ILogger<UserAppService> logger,
    IObjectMapper objectMapper,
    IQueryableAsyncExecuter asyncExecuter) : BaseAppService, IUserAppService
{
    /// <summary>
    /// 获取用户列表（分页）
    /// </summary>
    public async Task<PagedResultDto<UserOutputDto>> GetPagedListAsync(
        GetUserPagedInputDto input,
        CancellationToken cancellationToken = default)
    {
        var userQuery = await userRepository.GetQueryableAsync(cancellationToken);

        // 应用过滤条件
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            userQuery = userQuery.Where(u =>
                u.Username.Contains(input.Keyword) ||
                u.Email.Contains(input.Keyword));
        }

        if (input.IsActive.HasValue)
        {
            userQuery = userQuery.Where(u => u.IsActive == input.IsActive.Value);
        }

        // 获取总数
        var totalCount = await asyncExecuter.CountAsync(userQuery, cancellationToken);

        // 分页
        var users = await asyncExecuter.ToListAsync(userQuery
            .OrderBy(u => u.CreationTime)
            .Skip(input.Offset)
            .Take(input.Limit), cancellationToken);

        // 获取用户角色
        var userIds = users.Select(u => u.Id).ToList();
        var userRoles = await userRoleRepository
            .GetListAsync(ur => userIds.Contains(ur.UserId), cancellationToken);

        var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToList();
        var roles = await roleRepository
            .GetListAsync(r => roleIds.Contains(r.Id), cancellationToken);

        // 构建上下文
        var contextItems = new Dictionary<string, object>
        {
            ["UserRoles"] = userRoles,
            ["Roles"] = roles
        };

        // 映射 DTO (AutoMapper 自动处理角色关联)
        var userDtos = objectMapper.Map<List<User>, List<UserOutputDto>>(users, contextItems);

        return new PagedResultDto<UserOutputDto>(totalCount, userDtos);
    }

    /// <summary>
    /// 创建用户
    /// </summary>
    public async Task<UserOutputDto> CreateAsync(
        CreateUserInputDto input,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始创建用户 {Username}... 邮箱：{Email}", input.Username, input.Email);

        // 调用领域服务创建用户
        var user = await userDomainService.CreateUserWithRolesAsync(
            input.Username,
            input.Email,
            input.Password,
            input.Nickname,
            input.RoleIds,
            cancellationToken);

        // 获取用户角色
        var userRoles = await userRoleRepository
            .GetListAsync(ur => ur.UserId == user.Id, cancellationToken);

        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = await roleRepository
            .GetListAsync(r => roleIds.Contains(r.Id), cancellationToken);

        logger.LogInformation("创建用户成功 (ID: {Id})", user.Id);

        // ✅ 统一使用上下文传递
        var contextItems = new Dictionary<string, object>
        {
            ["UserRoles"] = userRoles,
            ["Roles"] = roles
        };

        return objectMapper.Map<User, UserOutputDto>(user, contextItems);
    }

    /// <summary>
    /// 更新用户
    /// </summary>
    public async Task<UserOutputDto> UpdateAsync(
        Guid id,
        UpdateUserInputDto input,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始更新用户 {Id}...", id);

        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"用户 {id} 不存在");
        }

        // 更新用户信息
        user.Update(input.Nickname, input.PhoneNumber, input.AvatarUrl);

        if (input.IsActive.HasValue)
        {
            if (input.IsActive.Value)
            {
                user.Enable();
            }
            else
            {
                user.Disable();
            }
        }

        await userRepository.UpdateAsync(user, cancellationToken);

        logger.LogInformation("更新用户成功 (ID: {Id})", user.Id);

        // 获取用户角色
        var userRoles = await userRoleRepository
            .GetListAsync(ur => ur.UserId == user.Id, cancellationToken);

        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = await roleRepository
            .GetListAsync(r => roleIds.Contains(r.Id), cancellationToken);

        // ✅ 统一使用上下文传递
        var contextItems = new Dictionary<string, object>
        {
            ["UserRoles"] = userRoles,
            ["Roles"] = roles
        };

        return objectMapper.Map<User, UserOutputDto>(user, contextItems);
    }

    /// <summary>
    /// 删除用户（软删除）
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始删除用户 {Id}...", id);

        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"用户 {id} 不存在");
        }

        // 级联软删除用户角色关系
        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == id, cancellationToken);
        if (userRoles.Any())
        {
            await userRoleRepository.DeleteManyAsync(userRoles, cancellationToken);
        }

        // 级联软删除外部登录连接
        var externalLogins = await externalLoginRepository.GetListAsync(el => el.UserId == id, cancellationToken);
        if (externalLogins.Any())
        {
            await externalLoginRepository.DeleteManyAsync(externalLogins, cancellationToken);
        }

        await userRepository.DeleteAsync(user, cancellationToken);

        logger.LogInformation("删除用户成功 (ID: {Id})", id);
    }
}

using System.Linq.Dynamic.Core;
using AiRelay.Application.Users.Dtos;
using AiRelay.Domain.Shared.Security.PasswordHash;
using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.Logging;

using Leistd.Security.Users;

namespace AiRelay.Application.Users.AppServices;

/// <summary>
/// 用户应用服务
/// </summary>
public class UserAppService(
    IRepository<User, Guid> userRepository,
    IRepository<Role, Guid> roleRepository,
    IRepository<UserRole, Guid> userRoleRepository,
    UserDomainService userDomainService,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    ILogger<UserAppService> logger,
    IObjectMapper objectMapper,
    IQueryableAsyncExecuter asyncExecuter) : BaseAppService, IUserAppService
{
    /// <summary>
    /// 获取用户列表（分页）
    /// </summary>
    public async Task<PagedResultDto<UserManagementOutputDto>> GetPagedListAsync(
        GetUserPagedInputDto input,
        CancellationToken cancellationToken = default)
    {
        var userQuery = await userRepository.GetQueryableAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            userQuery = userQuery.Where(u => u.Username.Contains(keyword) || u.Email.Contains(keyword) || (u.Nickname != null && u.Nickname.Contains(keyword)));
        }

        if (input.IsActive.HasValue)
        {
            userQuery = userQuery.Where(u => u.IsActive == input.IsActive.Value);
        }

        if (input.IsEmailVerified.HasValue)
        {
            userQuery = userQuery.Where(u => u.EmailConfirmed == input.IsEmailVerified.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Role))
        {
            var role = await roleRepository.GetFirstAsync(r => r.Name == input.Role.Trim(), q => q.OrderBy(r => r.Id), cancellationToken);
            if (role == null)
            {
                return new PagedResultDto<UserManagementOutputDto>(0, []);
            }

            var userRoleQuery = await userRoleRepository.GetQueryableAsync(cancellationToken);
            userQuery = userQuery.Where(u => userRoleQuery.Any(ur => ur.RoleId == role.Id && ur.UserId == u.Id));
        }

        var totalCount = await asyncExecuter.CountAsync(userQuery, cancellationToken);
        var sorting = input.Sorting ?? "username asc";
        userQuery = userQuery.OrderBy(sorting);
        var users = await asyncExecuter.ToListAsync(
            userQuery.Skip(input.Offset).Take(input.Limit),
            cancellationToken);

        var userDtos = await MapToOutputsAsync(users, cancellationToken);
        return new PagedResultDto<UserManagementOutputDto>(totalCount, userDtos);
    }

    /// <summary>
    /// 获取用户详情
    /// </summary>
    public async Task<UserManagementOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        return await MapToOutputAsync(user, cancellationToken);
    }

    /// <summary>
    /// 创建用户
    /// </summary>
    public async Task<UserManagementOutputDto> CreateAsync(CreateUserInputDto input, CancellationToken cancellationToken = default)
    {
        var username = input.Username.Trim();
        var email = input.Email.Trim();
        var displayName = input.DisplayName?.Trim();

        logger.LogInformation("开始创建用户 {Username}... 邮箱：{Email}", username, email);

        var roles = await GetRolesByNamesAsync(input.Roles, cancellationToken);
        var user = await userDomainService.CreateUserAsync(username, email, input.Password, displayName, cancellationToken);
        user.UpdateManagement(email, displayName, input.Avatar?.Trim(), input.IsActive, input.IsEmailVerified);
        await userRepository.UpdateAsync(user, cancellationToken);
        await AssignRolesAsync(user.Id, roles, cancellationToken);

        logger.LogInformation("创建用户成功 (ID: {Id})", user.Id);
        return await MapToOutputAsync(user, cancellationToken);
    }

    /// <summary>
    /// 更新用户
    /// </summary>
    public async Task<UserManagementOutputDto> UpdateAsync(Guid id, UpdateUserInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始更新用户 {Id}...", id);

        var user = await GetUserOrThrowAsync(id, cancellationToken);
        if (user.IsSuperAdmin && user.Id != currentUser.Id)
        {
            throw new BadRequestException("系统内置超级管理员不允许被其他管理员更新");
        }

        var email = input.Email.Trim();
        if (!await userDomainService.IsEmailAvailableAsync(id, email, cancellationToken))
        {
            throw new BadRequestException($"邮箱 '{email}' 已被使用");
        }

        var roles = await GetRolesByNamesAsync(input.Roles, cancellationToken);
        user.UpdateManagement(email, input.DisplayName?.Trim(), input.Avatar?.Trim(), input.IsActive, input.IsEmailVerified);
        await userRepository.UpdateAsync(user, cancellationToken);
        await ReplaceRolesAsync(user.Id, roles, cancellationToken);

        logger.LogInformation("更新用户成功 (ID: {Id})", user.Id);
        return await MapToOutputAsync(user, cancellationToken);
    }

    /// <summary>
    /// 启用用户
    /// </summary>
    public async Task EnableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        if (user.IsSuperAdmin && user.Id != currentUser.Id)
        {
            throw new BadRequestException("系统内置超级管理员不允许被其他管理员操作");
        }

        user.Enable();
        await userRepository.UpdateAsync(user, cancellationToken);
    }

    /// <summary>
    /// 禁用用户
    /// </summary>
    public async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        if (user.IsSuperAdmin && user.Id != currentUser.Id)
        {
            throw new BadRequestException("系统内置超级管理员不允许被其他管理员禁用");
        }
        if (user.IsSuperAdmin && user.Id == currentUser.Id)
        {
            throw new BadRequestException("系统内置超级管理员不允许禁用自己");
        }

        user.Disable();
        await userRepository.UpdateAsync(user, cancellationToken);
    }

    /// <summary>
    /// 重置用户密码
    /// </summary>
    public async Task ResetPasswordAsync(Guid id, ResetUserPasswordInputDto input, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(id, cancellationToken);
        if (user.IsSuperAdmin && user.Id != currentUser.Id)
        {
            throw new BadRequestException("系统内置超级管理员的密码不允许被其他管理员重置");
        }

        user.UpdatePasswordHash(passwordHasher.HashPassword(input.Password));
        await userRepository.UpdateAsync(user, cancellationToken);
    }

    /// <summary>
    /// 删除用户（软删除）
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始删除用户 {Id}...", id);

        var user = await GetUserOrThrowAsync(id, cancellationToken);
        if (user.IsSuperAdmin)
        {
            throw new BadRequestException("系统内置超级管理员不允许删除");
        }

        await userRepository.DeleteAsync(user, cancellationToken);
        logger.LogInformation("删除用户成功 (ID: {Id})", id);
    }

    private async Task<User> GetUserOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        return user ?? throw new NotFoundException($"用户 {id} 不存在");
    }

    private async Task<List<Role>> GetRolesByNamesAsync(List<string> roleNames, CancellationToken cancellationToken)
    {
        var normalizedRoleNames = roleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedRoleNames.Count == 0)
        {
            throw new BadRequestException("请至少选择一个角色");
        }

        var roles = (await roleRepository.GetListAsync(r => normalizedRoleNames.Contains(r.Name), cancellationToken)).ToList();
        var missingRoles = normalizedRoleNames.Except(roles.Select(r => r.Name), StringComparer.OrdinalIgnoreCase).ToList();
        if (missingRoles.Count != 0)
        {
            throw new BadRequestException($"角色不存在: {string.Join(", ", missingRoles)}");
        }

        return roles;
    }

    private async Task AssignRolesAsync(Guid userId, List<Role> roles, CancellationToken cancellationToken)
    {
        var userRoles = roles.Select(role => new UserRole(userId, role.Id)).ToList();
        await userRoleRepository.InsertManyAsync(userRoles, cancellationToken);
    }

    private async Task ReplaceRolesAsync(Guid userId, List<Role> roles, CancellationToken cancellationToken)
    {
        var currentRoles = (await userRoleRepository.GetListAsync(ur => ur.UserId == userId, cancellationToken)).ToList();
        if (currentRoles.Count != 0)
        {
            await userRoleRepository.DeleteManyAsync(currentRoles, cancellationToken);
        }

        await AssignRolesAsync(userId, roles, cancellationToken);
    }

    private async Task<List<UserManagementOutputDto>> MapToOutputsAsync(List<User> users, CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return [];
        }

        var userIds = users.Select(u => u.Id).ToList();
        var userRoles = (await userRoleRepository.GetListAsync(ur => userIds.Contains(ur.UserId), cancellationToken)).ToList();
        var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToList();
        var roles = roleIds.Count == 0 ? [] : (await roleRepository.GetListAsync(r => roleIds.Contains(r.Id), cancellationToken)).ToList();

        return objectMapper.Map<List<User>, List<UserManagementOutputDto>>(users, CreateMappingContext(userRoles, roles));
    }

    private async Task<UserManagementOutputDto> MapToOutputAsync(User user, CancellationToken cancellationToken)
    {
        var userRoles = (await userRoleRepository.GetListAsync(ur => ur.UserId == user.Id, cancellationToken)).ToList();
        var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToList();
        var roles = roleIds.Count == 0 ? [] : (await roleRepository.GetListAsync(r => roleIds.Contains(r.Id), cancellationToken)).ToList();
        return objectMapper.Map<User, UserManagementOutputDto>(user, CreateMappingContext(userRoles, roles));
    }

    private static Dictionary<string, object> CreateMappingContext(List<UserRole> userRoles, List<Role> roles)
    {
        return new Dictionary<string, object>
        {
            ["UserRoles"] = userRoles,
            ["Roles"] = roles
        };
    }

}

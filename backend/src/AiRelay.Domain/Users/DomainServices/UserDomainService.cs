using AiRelay.Domain.Shared.Security.PasswordHash;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.Users.DomainServices;

/// <summary>
/// 用户领域服务
/// </summary>
public class UserDomainService(
    IRepository<User, Guid> userRepository,
    IRepository<Role, Guid> roleRepository,
    IRepository<UserRole, Guid> userRoleRepository,
    IPasswordHasher passwordHasher,
    ILogger<UserDomainService> logger)
{
    /// <summary>
    /// 检查用户名是否可用
    /// </summary>
    public async Task<bool> IsUsernameAvailableAsync(string username, CancellationToken cancellationToken = default)
    {
        return !await userRepository.AnyAsync(u => u.Username == username, cancellationToken);
    }

    /// <summary>
    /// 检查邮箱是否可用
    /// </summary>
    public async Task<bool> IsEmailAvailableAsync(string email, CancellationToken cancellationToken = default)
    {
        return !await userRepository.AnyAsync(u => u.Email == email, cancellationToken);
    }

    /// <summary>
    /// 创��用户并分配角色
    /// </summary>
    public async Task<User> CreateUserWithRolesAsync(
        string username,
        string email,
        string password,
        string? nickname,
        List<Guid>? roleIds,
        CancellationToken cancellationToken = default)
    {
        // 检查用户名唯一性
        if (!await IsUsernameAvailableAsync(username, cancellationToken))
        {
            throw new BadRequestException($"用户名 '{username}' 已存在");
        }

        // 检查邮箱唯一性
        if (!await IsEmailAvailableAsync(email, cancellationToken))
        {
            throw new BadRequestException($"邮箱 '{email}' 已被使用");
        }

        // 哈希密码
        var passwordHash = passwordHasher.HashPassword(password);

        // 创建用户
        var user = new User(username, email, passwordHash, nickname);
        await userRepository.InsertAsync(user, cancellationToken);

        // 分配角色
        if (roleIds != null && roleIds.Count > 0)
        {
            await AssignRolesToUserAsync(user.Id, roleIds, cancellationToken);
        }

        logger.LogInformation("创建用户成功: {Username} (ID: {UserId})", user.Username, user.Id);
        return user;
    }

    /// <summary>
    /// 为用户分配角色
    /// </summary>
    public async Task AssignRolesToUserAsync(Guid userId, List<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var userRoles = roleIds.Select(roleId => new UserRole(userId, roleId)).ToList();
        await userRoleRepository.InsertManyAsync(userRoles, cancellationToken);
    }

    /// <summary>
    /// 为用户分配默认角色
    /// </summary>
    public async Task AssignDefaultRolesToUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var defaultRoles = (await roleRepository.GetListAsync(r => r.IsDefault, cancellationToken)).ToList();

        if (defaultRoles.Count == 0)
        {
            logger.LogWarning("未找到默认角色，用户 {UserId} 未分配任何角色", userId);
            return;
        }

        var userRoles = defaultRoles.Select(role => new UserRole(userId, role.Id)).ToList();
        await userRoleRepository.InsertManyAsync(userRoles, cancellationToken);

        logger.LogInformation("为用户 {UserId} 分配了 {Count} 个默认角色", userId, defaultRoles.Count);
    }

    /// <summary>
    /// 获取用户的角色名称列表
    /// </summary>
    public async Task<List<string>> GetUserRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == userId, cancellationToken);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();

        if (roleIds.Count == 0)
        {
            return [];
        }

        var roles = await roleRepository.GetListAsync(r => roleIds.Contains(r.Id), cancellationToken);
        return roles.Select(r => r.Name).ToList();
    }

    /// <summary>
    /// 验证用户凭据
    /// </summary>
    public async Task<User?> ValidateCredentialsAsync(
        string usernameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetFirstAsync(
            u => u.Username == usernameOrEmail || u.Email == usernameOrEmail,
            cancellationToken);

        if (user == null)
        {
            return null;
        }

        if (user.PasswordHash == null || !passwordHasher.VerifyPassword(user.PasswordHash, password))
        {
            return null;
        }

        return user;
    }
}

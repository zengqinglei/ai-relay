using AiRelay.Domain.Shared.Security.PasswordHash;
using AiRelay.Domain.Users.Entities;
using AiRelay.Domain.Users.Options;
using Leistd.Ddd.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.Initialization;

/// <summary>
/// 系统初始化器实现
/// </summary>
public class SystemInitializer(
    IRepository<User, Guid> userRepository,
    IRepository<Role, Guid> roleRepository,
    IRepository<UserRole, Guid> userRoleRepository,
    IPasswordHasher passwordHasher,
    IOptions<DefaultAdminOptions> adminOptions,
    ILogger<SystemInitializer> logger) : ISystemInitializer
{
    private const string AdminRoleName = "Admin";
    private const string MemberRoleName = "Member";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始初始化系统数据 ...");

        // 1. 初始化系统角色
        var (adminRole, memberRole) = await InitializeRolesAsync(cancellationToken);

        // 2. 初始化默认管理员用户
        await InitializeDefaultAdminAsync(adminRole, cancellationToken);

        // 注意：Admin 角色不需要分配权限
        // 在 PermissionChecker 中，Admin 角色自动拥有所有权限
        logger.LogInformation("Admin 角色通过代码逻辑自动拥有所有权限，无需插入数据库");

        logger.LogInformation("系统数据初始化完成");
    }

    /// <summary>
    /// 初始化系统角色
    /// </summary>
    private async Task<(Role AdminRole, Role MemberRole)> InitializeRolesAsync(CancellationToken cancellationToken)
    {
        // 检查 Admin 角色是否存在
        var adminRole = await roleRepository.GetFirstAsync(r => r.Name == AdminRoleName, cancellationToken);
        if (adminRole == null)
        {
            adminRole = new Role(
                name: AdminRoleName,
                displayName: "管理员",
                description: "系统管理员，拥有所有权限",
                isStatic: true,  // 系统内置角色，不可删除
                isDefault: false,
                sort: 1
            );
            await roleRepository.InsertAsync(adminRole, cancellationToken);
            logger.LogInformation("已创建系统角色: {RoleName}", AdminRoleName);
        }

        // 检查 Member 角色是否存在
        var memberRole = await roleRepository.GetFirstAsync(r => r.Name == MemberRoleName, cancellationToken);
        if (memberRole == null)
        {
            memberRole = new Role(
                name: MemberRoleName,
                displayName: "普通成员",
                description: "系统默认角色，新用户自动分配",
                isStatic: true,  // 系统内置角色，不可删除
                isDefault: true, // 默认角色，新用户自动分配
                sort: 100
            );
            await roleRepository.InsertAsync(memberRole, cancellationToken);
            logger.LogInformation("已创建系统角色: {RoleName}", MemberRoleName);
        }

        return (adminRole, memberRole);
    }

    /// <summary>
    /// 初始化默认管理员用户
    /// </summary>
    private async Task InitializeDefaultAdminAsync(Role adminRole, CancellationToken cancellationToken)
    {
        var options = adminOptions.Value;

        // 检查管理员用户是否存在
        var adminUser = await userRepository.GetFirstAsync(u => u.Username == options.Username, cancellationToken);
        if (adminUser != null)
        {
            logger.LogInformation("默认管理员用户已存在: {Username}", options.Username);
            return;
        }

        // 创建管理员用户
        var passwordHash = passwordHasher.HashPassword(options.Password);
        adminUser = new User(
            username: options.Username,
            email: options.Email,
            passwordHash: passwordHash,
            nickname: options.Nickname ?? "系统管理员"
        );

        await userRepository.InsertAsync(adminUser, cancellationToken);
        logger.LogInformation("已创建默认管理员用户: {Username}", options.Username);

        // 分配 Admin 角色
        var userRole = new UserRole(adminUser.Id, adminRole.Id);
        await userRoleRepository.InsertAsync(userRole, cancellationToken);
        logger.LogInformation("已为管理员用户分配 {RoleName} 角色", AdminRoleName);
    }
}

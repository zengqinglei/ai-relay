using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.Auth.DomainServices;

/// <summary>
/// 认证领域服务
/// </summary>
public class AuthDomainService(
    UserDomainService userDomainService,
    ILogger<AuthDomainService> logger)
{
    /// <summary>
    /// 认证用户
    /// </summary>
    public async Task<User> AuthenticateUserAsync(
        string usernameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("用户登录请求: {UsernameOrEmail}", usernameOrEmail);

        // 验证凭据
        var user = await userDomainService.ValidateCredentialsAsync(usernameOrEmail, password, cancellationToken);

        if (user == null)
        {
            logger.LogWarning("登录失败: 用户不存在或密码错误 - {UsernameOrEmail}", usernameOrEmail);
            throw new UnauthorizedException("用户名或密码错误");
        }

        // 检查用户状态
        ValidateUserStatus(user);

        // 记录登录成功
        user.RecordLoginSuccess();

        logger.LogInformation("用户登录成功: {Username} (ID: {UserId})", user.Username, user.Id);
        return user;
    }

    /// <summary>
    /// 验证用户状态
    /// </summary>
    private void ValidateUserStatus(User user)
    {
        if (!user.IsActive)
        {
            logger.LogWarning("登录失败: 用户已被禁用 - 用户: {Username} (ID: {UserId})",
                user.Username, user.Id);
            throw new UnauthorizedException("用户已被禁用");
        }

        if (user.IsLockedOut())
        {
            logger.LogWarning("登录失败: 用户已被锁定 - 用户: {Username} (ID: {UserId}), 锁定至: {LockoutEnd}",
                user.Username, user.Id, user.LockoutEnd);
            throw new UnauthorizedException("用户已被锁定，请稍后再试");
        }
    }
}

using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Leistd.Exception.Core;

namespace AiRelay.Domain.Auth.DomainServices;

/// <summary>
/// 认证领域服务
/// </summary>
public class AuthDomainService(UserDomainService userDomainService)
{
    /// <summary>
    /// 认证用户
    /// </summary>
    public async Task<User> AuthenticateUserAsync(
        string usernameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        // 验证凭据
        var user = await userDomainService.ValidateCredentialsAsync(usernameOrEmail, password, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedException($"登录失败: 用户不存在或密码错误 - {usernameOrEmail}");
        }

        // 检查用户状态
        ValidateUserStatus(user);

        // 记录登录成功
        user.RecordLoginSuccess();
        return user;
    }

    /// <summary>
    /// 验证用户状态
    /// </summary>
    private void ValidateUserStatus(User user)
    {
        if (!user.IsActive)
        {
            throw new UnauthorizedException($"登录失败: 用户已被禁用 - 用户: {user.Username}");
        }

        if (user.IsLockedOut())
        {
            throw new UnauthorizedException($"登录失败: 用户已被锁定 - 用户: {user.Username}, 锁定至: {user.LockoutEnd}");
        }
    }
}

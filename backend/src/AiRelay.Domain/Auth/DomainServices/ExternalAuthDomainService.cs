using AiRelay.Domain.Auth.Entities;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.Shared.OAuth.Authorize.ValueObjects;
using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.Auth.DomainServices;

/// <summary>
/// 外部认证领域服务
/// </summary>
public class ExternalAuthDomainService(
    IRepository<User, Guid> userRepository,
    IRepository<ExternalLoginConnection, Guid> externalLoginRepository,
    UserDomainService userDomainService,
    IServiceProvider serviceProvider,
    ILogger<ExternalAuthDomainService> logger)
{
    /// <summary>
    /// 使用外部提供商认证
    /// </summary>
    public async Task<(User user, OAuthTokenInfo tokenInfo)> AuthenticateWithProviderAsync(
        string provider,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始外部认证流程: Provider={Provider}", provider);

        // 1. 获取 OAuth 服务 (Keyed Service)
        var oauthProvider = serviceProvider.GetKeyedService<IOAuthProvider>(provider.ToLower())
            ?? throw new BadRequestException($"不支持的外部身份提供商: {provider}");

        // 2. 交换 Token
        var tokenInfo = await oauthProvider.ExchangeCodeForTokenAsync(
            code,
            redirectUri,
            cancellationToken: cancellationToken);

        // 3. 获取用户信息
        var externalUserInfo = await oauthProvider.GetUserInfoAsync(tokenInfo.AccessToken, cancellationToken);

        // 4. 查找或创建用户
        var user = await FindOrCreateUserAsync(provider, externalUserInfo, cancellationToken);

        // 5. 记录登录成功
        user.RecordLoginSuccess();
        await userRepository.UpdateAsync(user, cancellationToken);

        logger.LogInformation("外部认证成功: Provider={Provider}, Username={Username}", provider, user.Username);

        return (user, tokenInfo);
    }

    /// <summary>
    /// 查找或创建外部登录用户
    /// </summary>
    private async Task<User> FindOrCreateUserAsync(
        string provider,
        ExternalUserInfo externalUserInfo,
        CancellationToken cancellationToken)
    {
        // 1. 检查是否已存在外部登录连接
        var connection = await externalLoginRepository.GetFirstAsync(
            c => c.Provider == provider && c.ProviderUserId == externalUserInfo.ProviderId,
            cancellationToken);

        if (connection != null)
        {
            // 已有连接，返回已关联的用户
            var existingUser = await userRepository.GetByIdAsync(connection.UserId, cancellationToken);
            if (existingUser == null)
                throw new NotFoundException($"用户 '{connection.UserId}' 不存在");

            logger.LogInformation("用户 {Username} 通过 {Provider} 登录", existingUser.Username, provider);
            return existingUser;
        }

        // 2. 检查邮箱是否已存在用户
        User? user = null;
        if (!string.IsNullOrEmpty(externalUserInfo.Email))
        {
            user = await userRepository.GetFirstAsync(u => u.Email == externalUserInfo.Email, cancellationToken);
        }

        if (user == null)
        {
            // 3. 创建新用户（第一次通过外部登录）
            user = new User(
                username: externalUserInfo.Username,
                email: externalUserInfo.Email ?? $"{externalUserInfo.Username}@{provider.ToLower()}.local",
                passwordHash: null,  // 外部登录用户无密码
                nickname: externalUserInfo.Nickname ?? externalUserInfo.Username
            );

            if (!string.IsNullOrEmpty(externalUserInfo.AvatarUrl))
            {
                user.Update(user.Nickname, user.PhoneNumber, externalUserInfo.AvatarUrl);
            }

            await userRepository.InsertAsync(user, cancellationToken);
            logger.LogInformation("通过 {Provider} 创建新用户: {Username}", provider, user.Username);

            // 4. 分配默认角色
            await userDomainService.AssignDefaultRolesToUserAsync(user.Id, cancellationToken);
        }

        // 5. 创建外部登录连接
        var newConnection = new ExternalLoginConnection(
            userId: user.Id,
            provider: provider,
            providerUserId: externalUserInfo.ProviderId,
            providerUsername: externalUserInfo.Username,
            providerEmail: externalUserInfo.Email,
            providerAvatarUrl: externalUserInfo.AvatarUrl
        );
        await externalLoginRepository.InsertAsync(newConnection, cancellationToken);

        logger.LogInformation("已为用户 {Username} 创建 {Provider} 登录连接", user.Username, provider);

        return user;
    }
}

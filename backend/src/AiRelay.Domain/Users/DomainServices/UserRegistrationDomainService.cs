using AiRelay.Domain.Shared.Security.PasswordHash;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.Users.DomainServices;

/// <summary>
/// 用户注册领域服务
/// </summary>
public class UserRegistrationDomainService(
    IRepository<User, Guid> userRepository,
    IPasswordHasher passwordHasher,
    ILogger<UserRegistrationDomainService> logger)
{
    /// <summary>
    /// 注册新用户
    /// </summary>
    public async Task<User> RegisterUserAsync(
        string username,
        string email,
        string password,
        string? nickname = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始注册用户 {Username}... 邮箱：{Email}", username, email);

        // 验证用户名是否已存在
        var existingUser = await userRepository.GetFirstAsync(u => u.Username == username, cancellationToken);
        if (existingUser != null)
            throw new BadRequestException($"用户名 '{username}' 已存在");

        // 验证邮箱是否已存在
        existingUser = await userRepository.GetFirstAsync(u => u.Email == email, cancellationToken);
        if (existingUser != null)
            throw new BadRequestException($"邮箱 '{email}' 已被使用");

        // 创建用户
        var passwordHash = passwordHasher.HashPassword(password);
        var user = new User(username, email, passwordHash, nickname ?? username);

        await userRepository.InsertAsync(user, cancellationToken);
        logger.LogInformation("用户注册成功: {Username} (ID: {UserId})", user.Username, user.Id);

        return user;
    }
}

using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.Security.Users;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.Auth.AppServices;

public class AuthAppService(
    IRepository<User, Guid> userRepository,
    UserDomainService userDomainService,
    ICurrentUser currentUser,
    ILogger<AuthAppService> logger) : BaseAppService(), IAuthAppService
{

    public async Task<UserOutputDto> RegisterAsync(RegisterInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始注册用户 {Username}... 邮箱：{Email}", input.Username, input.Email);

        var user = await userDomainService.CreateUserAsync(input.Username, input.Email, input.Password, input.Nickname, cancellationToken);
        await userDomainService.AssignDefaultRolesToUserAsync(user.Id, cancellationToken);

        logger.LogInformation("用户注册成功 (ID: {Id})", user.Id);

        return await GetCurrentUserOutputAsync(user.Id, cancellationToken);
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    public async Task<UserOutputDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id!.Value;
        return await GetCurrentUserOutputAsync(userId, cancellationToken);
    }

    /// <summary>
    /// 更新个人信息
    /// </summary>
    public async Task<UserOutputDto> UpdateCurrentUserAsync(UpdateCurrentUserInputDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id!.Value;
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"用户 '{userId}' 不存在");
        }

        logger.LogInformation("开始更新当前用户资料 (ID: {UserId})", user.Id);

        await userDomainService.UpdateProfileAsync(
            user,
            input.Username,
            input.Email,
            input.Nickname,
            input.PhoneNumber,
            input.Avatar,
            cancellationToken);

        await userRepository.UpdateAsync(user, cancellationToken);
        logger.LogInformation("更新当前用户资料成功 (ID: {UserId})", user.Id);

        return await GetCurrentUserOutputAsync(user.Id, cancellationToken);
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    public async Task ChangePasswordAsync(ChangePasswordInputDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id!.Value;
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"用户 '{userId}' 不存在");
        }

        logger.LogInformation("开始修改当前用户密码 (ID: {UserId})", user.Id);

        await userDomainService.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword, cancellationToken);
        await userRepository.UpdateAsync(user, cancellationToken);

        logger.LogInformation("修改当前用户密码成功 (ID: {UserId})", user.Id);
    }

    private async Task<UserOutputDto> GetCurrentUserOutputAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"用户 '{userId}' 不存在");
        }

        var roleNames = await userDomainService.GetUserRoleNamesAsync(userId, cancellationToken);

        return new UserOutputDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Nickname = user.Nickname,
            Avatar = user.Avatar,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            CreationTime = user.CreationTime,
            Roles = [.. roleNames]
        };
    }
}



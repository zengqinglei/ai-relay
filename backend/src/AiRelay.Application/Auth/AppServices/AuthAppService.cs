using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Auth.DomainServices;
using AiRelay.Domain.Shared.Security.Jwt;
using AiRelay.Domain.Shared.Security.Jwt.Options;
using AiRelay.Domain.Users.DomainServices;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Leistd.Security.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.Auth.AppServices;

public class AuthAppService(
    IRepository<User, Guid> userRepository,
    IRepository<UserRole, Guid> userRoleRepository,
    IRepository<Role, Guid> roleRepository,
    UserDomainService userDomainService,
    AuthDomainService authDomainService,    IJwtTokenProvider jwtTokenProvider,
    IOptions<JwtOptions> jwtOptions,
    ICurrentUser currentUser,
    IObjectMapper objectMapper,
    ILogger<AuthAppService> logger) : BaseAppService(), IAuthAppService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    /// <summary>
    /// 用户登录
    /// </summary>
    public async Task<LoginOutputDto> LoginAsync(LoginInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("用户 {UsernameOrEmail} 尝试登录...", input.UsernameOrEmail);

        // 调用领域服务进行认证
        var user = await authDomainService.AuthenticateUserAsync(input.UsernameOrEmail, input.Password, cancellationToken);
        // 更新用户登录状态
        await userRepository.UpdateAsync(user, cancellationToken);

        var roleNames = await userDomainService.GetUserRoleNamesAsync(user.Id, cancellationToken);

        // 生成 JWT Token
        var accessToken = jwtTokenProvider.GenerateToken(user.Id, user.Username, user.Email, [.. roleNames]);
        var refreshToken = jwtTokenProvider.GenerateRefreshToken();
        var expiryMinutes = _jwtOptions.ExpiryMinutes;

        logger.LogInformation("用户登录成功 (ID: {UserId})", user.Id);

        // 构建响应
        return new LoginOutputDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiryMinutes * 60
        };
    }

    public async Task<LoginOutputDto> RegisterAsync(RegisterInputDto input, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始注册用户 {Username}... 邮箱：{Email}", input.Username, input.Email);

        // 调用领域服务进行注册
        var user = await userDomainService.CreateUserAsync(input.Username, input.Email, input.Password, input.Nickname, cancellationToken);
        // 分配默认角色
        await userDomainService.AssignDefaultRolesToUserAsync(user.Id, cancellationToken);

        // 获取用户角色
        var roleNames = await userDomainService.GetUserRoleNamesAsync(user.Id, cancellationToken);

        // 生成 JWT Token
        var accessToken = jwtTokenProvider.GenerateToken(user.Id, user.Username, user.Email, roleNames.ToArray());
        var refreshToken = jwtTokenProvider.GenerateRefreshToken();
        var expiryMinutes = _jwtOptions.ExpiryMinutes;

        logger.LogInformation("用户注册成功 (ID: {Id})", user.Id);

        // 构建响应
        return new LoginOutputDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiryMinutes * 60
        };
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    public async Task<UserOutputDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id!.Value;
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"用户 '{userId}' 不存在");
        }

        return await MapUserAsync(user, cancellationToken);
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

        return await MapUserAsync(user, cancellationToken);
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

    private async Task<UserOutputDto> MapUserAsync(User user, CancellationToken cancellationToken)
    {
        var userRoles = await userRoleRepository.GetListAsync(ur => ur.UserId == user.Id, cancellationToken);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = roleIds.Count == 0 ? [] : await roleRepository.GetListAsync(r => roleIds.Contains(r.Id), cancellationToken);

        // ✅ 统一使用上下文传递
        var contextItems = new Dictionary<string, object>
        {
            ["UserRoles"] = userRoles,
            ["Roles"] = roles
        };

        return objectMapper.Map<User, UserOutputDto>(user, contextItems);
    }
}



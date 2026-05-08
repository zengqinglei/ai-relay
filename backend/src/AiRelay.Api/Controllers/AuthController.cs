using System.Security.Claims;
using AiRelay.Application.Auth.AppServices;
using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Users.DomainServices;
using Leistd.Ddd.Domain.Repositories;
using AiRelay.Domain.Users.Entities;
using Leistd.Exception.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 认证控制器
/// </summary>
[Route("api/v1/auth")]
public class AuthController(
    IAuthAppService authService,
    ICaptchaAppService captchaAppService,
    IEmailVerificationAppService emailVerificationAppService,
    UserDomainService userDomainService,
    IRepository<User, Guid> userRepository,
    Microsoft.Extensions.Options.IOptions<AiRelay.Domain.Users.Options.UserRegistrationOptions> securityOptions) : BaseController
{
    [AllowAnonymous]
    [HttpPost("session-login")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SessionLoginAsync([FromBody] LoginInputDto request, CancellationToken cancellationToken)
    {
        // 验证凭据
        var user = await userDomainService.ValidateCredentialsAsync(
            request.UsernameOrEmail,
            request.Password,
            cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedException($"登录失败: 用户不存在或密码错误 - {request.UsernameOrEmail}");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException($"登录失败: 用户已被禁用 - 用户: {user.Username}");
        }

        if (user.IsLockedOut())
        {
            throw new UnauthorizedException($"登录失败: 用户已被锁定 - 用户: {user.Username}, 锁定至: {user.LockoutEnd}");
        }

        // 记录登录成功并建立 Cookie 会话
        user.RecordLoginSuccess();
        await userRepository.UpdateAsync(user, cancellationToken);

        var identity = new ClaimsIdentity("AiRelayCookie");
        identity.AddClaim(new Claim(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));

        await HttpContext.SignInAsync("AiRelayCookie", new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        await HttpContext.SignOutAsync("AiRelayCookie");
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("security-config")]
    public Task<SecurityConfigOutputDto> GetSecurityConfigAsync()
    {
        return Task.FromResult(new SecurityConfigOutputDto
        {
            EnableEmailVerification = securityOptions.Value.EnableEmailVerification
        });
    }

    [AllowAnonymous]
    [HttpGet("captcha")]
    public async Task<CaptchaOutputDto> GetCaptchaAsync(CancellationToken cancellationToken)
    {
        return await captchaAppService.GenerateCaptchaAsync(cancellationToken);
    }

    [AllowAnonymous]
    [HttpPost("send-email-code")]
    public async Task SendEmailCodeAsync([FromBody] SendEmailCodeInputDto request, CancellationToken cancellationToken)
    {
        await emailVerificationAppService.SendEmailCodeAsync(request, cancellationToken);
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<UserOutputDto> RegisterAsync([FromBody] RegisterInputDto request, CancellationToken cancellationToken)
    {
        return await authService.RegisterAsync(request, cancellationToken);
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<UserOutputDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        return await authService.GetCurrentUserAsync(cancellationToken);
    }

    /// <summary>
    /// 更新个人信息
    /// </summary>
    [Authorize]
    [HttpPut("me")]
    public async Task<UserOutputDto> UpdateCurrentUserAsync([FromBody] UpdateCurrentUserInputDto request, CancellationToken cancellationToken)
    {
        return await authService.UpdateCurrentUserAsync(request, cancellationToken);
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task ChangePasswordAsync([FromBody] ChangePasswordInputDto request, CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(request, cancellationToken);
    }
}

using AiRelay.Application.Auth.AppServices;
using AiRelay.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 认证控制器
/// </summary>
[Route("api/v1/auth")]
public class AuthController(IAuthAppService authService) : BaseController
{
    /// <summary>
    /// 用户登录
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<LoginOutputDto> LoginAsync([FromBody] LoginInputDto request, CancellationToken cancellationToken)
    {
        return await authService.LoginAsync(request, cancellationToken);
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<LoginOutputDto> RegisterAsync([FromBody] RegisterInputDto request, CancellationToken cancellationToken)
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
}

using AiRelay.Application.Auth.AppServices;
using AiRelay.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 外部认证控制器
/// </summary>
[Route("api/v1/external-auth")]
public class ExternalAuthController(IExternalAuthAppService externalAuthAppService) : BaseController
{
    /// <summary>
    /// 获取外部登录 URL（GitHub, Google）
    /// </summary>
    /// <param name="provider">提供商名称（github, google）</param>
    [AllowAnonymous]
    [HttpGet("{provider}/login-url")]
    public ExternalLoginUrlOutputDto GetLoginUrl(string provider)
    {
        return externalAuthAppService.GetLoginUrl(provider);
    }

    /// <summary>
    /// 处理外部登录回调
    /// </summary>
    /// <param name="provider">提供商名称（github, google）</param>
    /// <param name="request">回调请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    [AllowAnonymous]
    [HttpPost("{provider}/callback")]
    public async Task<LoginOutputDto> CallbackAsync(
        string provider,
        [FromBody] ExternalLoginCallbackInputDto request,
        CancellationToken cancellationToken)
    {
        return await externalAuthAppService.HandleCallbackAsync(provider, request, cancellationToken);
    }
}

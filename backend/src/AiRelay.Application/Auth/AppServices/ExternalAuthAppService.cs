using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Auth.DomainServices;
using AiRelay.Domain.Auth.Options;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.Users.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Exception.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiRelay.Application.Auth.AppServices;

/// <summary>
/// 外部身份验证服务
/// </summary>
public class ExternalAuthAppService(
    ExternalAuthDomainService externalAuthDomainService,
    IServiceProvider serviceProvider,
    IOptions<ExternalAuthOptions> externalAuthOptions) : BaseAppService(), IExternalAuthAppService
{
    private readonly ExternalAuthOptions _externalAuthOptions = externalAuthOptions.Value;
    /// <summary>
    /// 获取外部登录 URL
    /// </summary>
    public ExternalLoginUrlOutputDto GetLoginUrl(string provider)
    {
        var providerConfig = _externalAuthOptions.GetProviderConfig(provider)
            ?? throw new NotFoundException($"外部身份提供商 {provider} 未配置");

        var redirectUri = providerConfig.RedirectUri
            ?? throw new NotFoundException($"外部身份提供商 {provider} RedirectUri 未配置");

        var state = Guid.NewGuid().ToString("N");

        // 获取 Keyed OAuth 服务
        var oauthProvider = serviceProvider.GetKeyedService<IOAuthProvider>(provider.ToLower())
            ?? throw new BadRequestException($"不支持的外部身份提供商: {provider}");

        var loginUrl = oauthProvider.GetAuthorizationUrl(redirectUri, state);

        return new ExternalLoginUrlOutputDto
        {
            LoginUrl = loginUrl,
            State = state
        };
    }

    /// <summary>
    /// 处理外部登录回调
    /// </summary>
    /// <param name="provider">外部身份提供商（github, google）</param>
    /// <param name="request">回调请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<User> AuthenticateExternalUserAsync(string provider, ExternalLoginCallbackInputDto request, CancellationToken cancellationToken = default)
    {
        var providerConfig = _externalAuthOptions.GetProviderConfig(provider)
            ?? throw new NotFoundException($"外部身份提供商 {provider} 未配置");

        var redirectUri = providerConfig.RedirectUri
            ?? throw new NotFoundException($"外部身份提供商 {provider} RedirectUri 未配置");

        var (user, _) = await externalAuthDomainService.AuthenticateWithProviderAsync(
            provider,
            request.Code,
            redirectUri,
            cancellationToken);

        return user;
    }
}

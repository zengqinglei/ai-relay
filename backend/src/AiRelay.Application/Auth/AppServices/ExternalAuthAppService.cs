using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Auth.DomainServices;
using AiRelay.Domain.Auth.Options;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.Shared.Security.Jwt;
using AiRelay.Domain.Shared.Security.Jwt.Options;
using AiRelay.Domain.Users.DomainServices;
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
    UserDomainService userDomainService,
    IJwtTokenProvider jwtTokenProvider,
    IServiceProvider serviceProvider,
    IOptions<ExternalAuthOptions> externalAuthOptions,
    IOptions<JwtOptions> jwtOptions) : BaseAppService(), IExternalAuthAppService
{
    private readonly ExternalAuthOptions _externalAuthOptions = externalAuthOptions.Value;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
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
    public async Task<LoginOutputDto> HandleCallbackAsync(string provider, ExternalLoginCallbackInputDto request, CancellationToken cancellationToken = default)
    {
        // 获取配置的 RedirectUri
        var providerConfig = _externalAuthOptions.GetProviderConfig(provider)
            ?? throw new NotFoundException($"外部身份提供商 {provider} 未配置");

        var redirectUri = providerConfig.RedirectUri
            ?? throw new NotFoundException($"外部身份提供商 {provider} RedirectUri 未配置");

        // 调用领域服务进行外部认证
        var (user, oauthTokenInfo) = await externalAuthDomainService.AuthenticateWithProviderAsync(
            provider,
            request.Code,
            redirectUri,
            cancellationToken);

        // 获取用户角色
        var roleNames = await userDomainService.GetUserRoleNamesAsync(user.Id, cancellationToken);

        // 生成 JWT Token
        var jwtAccessToken = jwtTokenProvider.GenerateToken(user.Id, user.Username, user.Email, roleNames.ToArray());
        var refreshToken = jwtTokenProvider.GenerateRefreshToken();

        // 构建响应
        return new LoginOutputDto
        {
            AccessToken = jwtAccessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtOptions.ExpiryMinutes * 60
        };
    }
}

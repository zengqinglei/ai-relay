using AiRelay.Application.Auth.Dtos;
using Leistd.Ddd.Application.Contracts.AppService;

namespace AiRelay.Application.Auth.AppServices;

/// <summary>
/// 外部身份验证服务接口
/// </summary>
public interface IExternalAuthAppService : IAppService
{
    /// <summary>
    /// 获取外部登录 URL
    /// </summary>
    ExternalLoginUrlOutputDto GetLoginUrl(string provider);

    /// <summary>
    /// 处理外部登录回调
    /// </summary>
    /// <param name="provider">外部身份提供商（github, google）</param>
    /// <param name="request">回调请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<LoginOutputDto> HandleCallbackAsync(string provider, ExternalLoginCallbackInputDto request, CancellationToken cancellationToken = default);
}

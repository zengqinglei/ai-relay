using AiRelay.Application.Auth.Dtos;
using Leistd.Ddd.Application.Contracts.AppService;

namespace AiRelay.Application.Auth.AppServices;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthAppService : IAppService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    Task<LoginOutputDto> LoginAsync(LoginInputDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 用户注册
    /// </summary>
    Task<LoginOutputDto> RegisterAsync(RegisterInputDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    Task<UserOutputDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

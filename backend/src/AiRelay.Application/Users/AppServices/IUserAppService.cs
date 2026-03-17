using AiRelay.Application.Auth.Dtos;
using AiRelay.Application.Users.Dtos;
using Leistd.Ddd.Application.Contracts.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.Users.AppServices;

/// <summary>
/// 用户应用服务接口
/// </summary>
public interface IUserAppService : IAppService
{
    /// <summary>
    /// 获取用户列表（分页）
    /// </summary>
    Task<PagedResultDto<UserOutputDto>> GetPagedListAsync(
        GetUserPagedInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建用户
    /// </summary>
    Task<UserOutputDto> CreateAsync(
        CreateUserInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新用户
    /// </summary>
    Task<UserOutputDto> UpdateAsync(
        Guid id,
        UpdateUserInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除用户（软删除）
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

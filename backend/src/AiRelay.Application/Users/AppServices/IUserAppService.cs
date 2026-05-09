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
    Task<PagedResultDto<UserManagementOutputDto>> GetPagedListAsync(
        GetUserPagedInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户详情
    /// </summary>
    Task<UserManagementOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建用户
    /// </summary>
    Task<UserManagementOutputDto> CreateAsync(
        CreateUserInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新用户
    /// </summary>
    Task<UserManagementOutputDto> UpdateAsync(
        Guid id,
        UpdateUserInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 启用用户
    /// </summary>
    Task EnableAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 禁用用户
    /// </summary>
    Task DisableAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置用户密码
    /// </summary>
    Task ResetPasswordAsync(Guid id, ResetUserPasswordInputDto input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除用户（软删除）
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

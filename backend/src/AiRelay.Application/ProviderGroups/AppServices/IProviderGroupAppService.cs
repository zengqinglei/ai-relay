using AiRelay.Application.ProviderGroups.Dtos;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Application.Services;

namespace AiRelay.Application.ProviderGroups.AppServices;

/// <summary>
/// 提供商分组应用服务接口
/// </summary>
public interface IProviderGroupAppService : IApplicationService
{
    #region 分组管理

    Task<ProviderGroupOutputDto> CreateAsync(CreateProviderGroupInputDto input, CancellationToken cancellationToken = default);

    Task<ProviderGroupOutputDto> UpdateAsync(Guid id, UpdateProviderGroupInputDto input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProviderGroupOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResultDto<ProviderGroupOutputDto>> GetPagedListAsync(GetProviderGroupPagedInputDto input, CancellationToken cancellationToken = default);

    #endregion
}
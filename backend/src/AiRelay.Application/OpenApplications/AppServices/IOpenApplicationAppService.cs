using AiRelay.Application.OpenApplications.Dtos;
using Leistd.Ddd.Application.Contracts.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.OpenApplications.AppServices;

public interface IOpenApplicationAppService : IAppService
{
    Task<PagedResultDto<OpenApplicationOutputDto>> GetPagedListAsync(
        GetOpenApplicationPagedInputDto input,
        CancellationToken cancellationToken = default);

    Task<OpenApplicationOutputDto> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<OpenApplicationOutputDto> CreateAsync(
        CreateOpenApplicationInputDto input,
        CancellationToken cancellationToken = default);

    Task<OpenApplicationOutputDto> UpdateAsync(
        string id,
        UpdateOpenApplicationInputDto input,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<ResetOpenApplicationSecretOutputDto> ResetSecretAsync(string id, CancellationToken cancellationToken = default);
}

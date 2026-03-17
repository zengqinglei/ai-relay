using AiRelay.Application.ApiKeys.Dtos;
using Leistd.Ddd.Application.Contracts.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.ApiKeys.AppServices;

public interface IApiKeyAppService : IAppService
{
    Task<ApiKeyOutputDto> CreateAsync(CreateApiKeyInputDto input, CancellationToken cancellationToken = default);

    Task<ApiKeyOutputDto> UpdateAsync(Guid id, UpdateApiKeyInputDto input, CancellationToken cancellationToken = default);

    Task<ApiKeyValidationResult> ValidateAsync(string plainKey, CancellationToken cancellationToken = default);

    Task<ApiKeyOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResultDto<ApiKeyOutputDto>> GetPagedListAsync(GetApiKeyPagedInputDto input, CancellationToken cancellationToken = default);

    Task EnableAsync(Guid id, DateTime? newExpiresAt = null, CancellationToken cancellationToken = default);

    Task DisableAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

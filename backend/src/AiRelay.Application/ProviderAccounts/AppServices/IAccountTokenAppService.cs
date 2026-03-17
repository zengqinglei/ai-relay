using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using Leistd.Ddd.Application.Contracts.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.ProviderAccounts.AppServices;

public interface IAccountTokenAppService : IAppService
{
    IAsyncEnumerable<ChatStreamEvent> DebugModelAsync(
        Guid id,
        ChatMessageInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定平台的可用模型列表
    /// </summary>
    Task<IReadOnlyList<ModelOptionOutputDto>> GetAvailableModelsAsync(
        ProviderPlatform platform,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户分页列表
    /// </summary>
    Task<PagedResultDto<AccountTokenOutputDto>> GetPagedListAsync(
        GetAccountTokenPagedInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户详情
    /// </summary>
    Task<AccountTokenOutputDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 OAuth 授权链接
    /// </summary>
    Task<OAuthUrlOutputDto> GetOAuthUrlAsync(
        GetAuthUrlInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建账户
    /// </summary>
    Task<AccountTokenOutputDto> CreateAsync(
        CreateAccountTokenInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新账户
    /// </summary>
    Task<AccountTokenOutputDto> UpdateAsync(
        Guid id,
        UpdateAccountTokenInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除账户
    /// </summary>
    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 启用账户
    /// </summary>
    Task EnableAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 禁用账户
    /// </summary>
    Task DisableAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置账户状态（限流/异常）
    /// </summary>
    Task ResetStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

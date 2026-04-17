using AiRelay.Application.ProviderGroups.Dtos;
using Leistd.Ddd.Application.Contracts.AppService;

namespace AiRelay.Application.ProviderGroups.AppServices;

public interface ISmartProxyAppService : IAppService
{
    /// <summary>
    /// 选择代理账号（包含分组倍率信息）
    /// </summary>
    Task<SelectAccountResultDto> SelectAccountAsync(
        SelectProxyAccountInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理请求成功。
    /// 按账户模式下清除整账号熔断；按模型模式下仅清理当前上游模型熔断。
    /// </summary>
    Task HandleSuccessAsync(
        Guid accountId,
        string? upModelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理请求失败（更新账户状态：熔断/禁用）
    /// </summary>
    Task HandleFailureAsync(
        HandleFailureInputDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查账号是否在熔断期（供并发请求感知）
    /// </summary>
    Task<bool> IsRateLimitedAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}

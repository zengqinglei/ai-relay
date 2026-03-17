using Leistd.Ddd.Application.Contracts.AppService;

namespace AiRelay.Application.ProviderAccounts.AppServices;

/// <summary>
/// 账号配额应用服务接口
/// </summary>
public interface IAccountQuotaAppService : IAppService
{
    /// <summary>
    /// 刷新所有支持配额查询的账号
    /// </summary>
    Task RefreshAllQuotasAsync(CancellationToken cancellationToken = default);
}

using AiRelay.Domain.ProviderAccounts.Events;
using Leistd.EventBus.Core.EventHandler;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ProviderAccounts.EventHandlers;

/// <summary>
/// 账号恢复事件处理器
/// </summary>
public class AccountRecoveredEventHandler(
    ILogger<AccountRecoveredEventHandler> logger) : IEventHandler<AccountRecoveredEvent>
{
    public Task HandleAsync(AccountRecoveredEvent @event, CancellationToken cancellationToken = default)
    {
        // 记录审计日志
        logger.LogInformation(
            "【账号恢复】账号 {AccountId} 已从错误状态恢复",
            @event.AccountId);

        // TODO: 记录恢复指标
        // await metricsCollector.RecordAccountRecoveryAsync(...);

        return Task.CompletedTask;
    }
}

using AiRelay.Domain.ProviderAccounts.Events;
using Leistd.EventBus.Core.EventHandler;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ProviderAccounts.EventHandlers;

/// <summary>
/// 账号禁用事件处理器
/// </summary>
public class AccountDisabledEventHandler(
    ILogger<AccountDisabledEventHandler> logger) : IEventHandler<AccountDisabledEvent>
{
    public Task HandleAsync(AccountDisabledEvent @event, CancellationToken cancellationToken = default)
    {
        // 记录审计日志
        logger.LogWarning(
            "【账号禁用】账号 {AccountId} 已被系统自动禁用，状态码: {StatusCode}，原因: {Reason}",
            @event.AccountId,
            @event.StatusCode,
            @event.Reason);

        // TODO: 发送告警通知（邮件、钉钉、Slack 等）
        // await notificationService.SendAlertAsync(...);

        // TODO: 触发备用账号激活流程
        // await accountActivationService.ActivateBackupAccountAsync(...);

        return Task.CompletedTask;
    }
}

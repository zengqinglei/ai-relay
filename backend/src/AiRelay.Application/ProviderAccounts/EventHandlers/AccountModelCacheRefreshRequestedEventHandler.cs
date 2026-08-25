using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.Events;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Domain.Repositories;
using Leistd.EventBus.Core.EventHandler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ProviderAccounts.EventHandlers;

/// <summary>
/// 账号模型缓存刷新事件处理器
/// </summary>
public class AccountModelCacheRefreshRequestedEventHandler(
    IServiceProvider serviceProvider,
    ILogger<AccountModelCacheRefreshRequestedEventHandler> logger) : IEventHandler<AccountModelCacheRefreshRequestedEvent>
{
    public Task HandleAsync(AccountModelCacheRefreshRequestedEvent @event, CancellationToken cancellationToken = default)
    {
        // Fire-And-Forget 任务：不依赖事件总线 cancellationToken，避免事件处理完成后 token 被取消
        _ = Task.Run(async () =>
        {
            string? accountName = null;
            try
            {
                // 延迟等待事务真正提交，再执行缓存刷新
                await Task.Delay(2000);

                using var scope = serviceProvider.CreateScope();
                var domainService = scope.ServiceProvider.GetRequiredService<AccountTokenDomainService>();
                var repo = scope.ServiceProvider.GetRequiredService<IRepository<AccountToken, Guid>>();

                var account = await repo.GetByIdAsync(@event.AccountId);
                if (account != null)
                {
                    accountName = account.Name;
                    if (account.Status != AccountStatus.Error)
                    {
                        // 若未配置白名单，则触发上游刷新
                        if (account.ModelWhites == null || account.ModelWhites.Count == 0)
                        {
                            await domainService.RefreshTokenIfNeededAsync(account);
                            await domainService.FetchAndCacheUpstreamModelsAsync(account);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "异步刷新账户模型缓存失败: AccountName={AccountName}, AccountId={AccountId}", accountName ?? "Unknown", @event.AccountId);
            }
        });

        return Task.CompletedTask;
    }
}

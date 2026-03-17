using AiRelay.Application.ProviderAccounts.AppServices;

namespace AiRelay.Api.HostedServices.BackgroundServices;

/// <summary>
/// 账号配额刷新后台服务
/// </summary>
public class AccountQuotaRefreshHostedService(
    IServiceProvider serviceProvider,
    ILogger<AccountQuotaRefreshHostedService> logger) : BackgroundService
{
    private const int REFRESH_INTERVAL_MINUTES = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("账户配额刷新后台服务已启动");

        // 等待应用完全启动
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var quotaAppService = scope.ServiceProvider.GetRequiredService<IAccountQuotaAppService>();

                // 委托给应用服务
                await quotaAppService.RefreshAllQuotasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "刷新账户配额时发生错误");
            }

            await Task.Delay(TimeSpan.FromMinutes(REFRESH_INTERVAL_MINUTES), stoppingToken);
        }

        logger.LogInformation("账户配额刷新后台服务已停止");
    }
}

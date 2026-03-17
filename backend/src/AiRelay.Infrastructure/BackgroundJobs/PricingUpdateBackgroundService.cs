using AiRelay.Domain.UsageRecords.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.BackgroundJobs;

public class PricingUpdateBackgroundService(
    IPricingProvider pricingProvider,  // ✅ 依赖接口而不是具体类
    ILogger<PricingUpdateBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动时立即尝试更新一次
        await UpdateAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await UpdateAsync(stoppingToken);
        }
    }

    private async Task UpdateAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("开始更新模型价格表...");
            await pricingProvider.UpdatePricingCacheAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "后台任务更新模型价格表失败");
        }
    }
}

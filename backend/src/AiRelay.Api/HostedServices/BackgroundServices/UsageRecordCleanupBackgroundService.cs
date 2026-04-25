using AiRelay.Domain.UsageRecords.DomainServices;
using AiRelay.Domain.UsageRecords.Options;
using Microsoft.Extensions.Options;

namespace AiRelay.Api.HostedServices.BackgroundServices;

/// <summary>
/// 使用记录自动清理后台服务
/// 每天凌晨 02:00 执行，按保留天数清理过期的日志数据
/// </summary>
public class UsageRecordCleanupBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<UsageCleanupOptions> options,
    ILogger<UsageRecordCleanupBackgroundService> logger) : BackgroundService
{
    private readonly UsageCleanupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("使用记录自动清理已禁用，跳过执行");
            return;
        }

        logger.LogInformation(
            "使用记录自动清理服务已启动，摘要保留 {RetentionDays} 天，详情保留 {DetailRetentionDays} 天",
            _options.RetentionDays, _options.DetailRetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            // 计算距下次凌晨 02:00 的等待时间
            var now = DateTimeOffset.Now;
            var nextRun = now.Date.AddDays(1).AddHours(2);
            var delay = nextRun - now;

            logger.LogDebug("下次清理时间: {NextRun}（等待 {DelayMinutes} 分钟）", nextRun, (int)delay.TotalMinutes);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunCleanupAsync(stoppingToken);
        }

        logger.LogInformation("使用记录自动清理服务已停止");
    }

    private async Task RunCleanupAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("开始执行使用记录清理任务...");
        try
        {
            using var scope = serviceProvider.CreateScope();
            var domainService = scope.ServiceProvider.GetRequiredService<UsageRecordDomainService>();

            await domainService.CleanupExpiredRecordsAsync(
                _options.DetailRetentionDays,
                _options.RetentionDays,
                stoppingToken);

            logger.LogInformation("清理任务执行成功");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "使用记录清理任务执行失败: {Message}", ex.Message);
        }
    }
}

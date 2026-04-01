using AiRelay.Domain.Shared.ExternalServices.ModelClient.SignatureCache;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.BackgroundJobs;

/// <summary>
/// 签名缓存清理后台服务
/// </summary>
/// <remarks>
/// 每 5 分钟执行一次清理，移除过期的签名缓存
/// </remarks>
public sealed class SignatureCacheCleanupBackgroundService(
    ISignatureCache signatureCache,
    ILogger<SignatureCacheCleanupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("签名缓存清理服务已启动，清理间隔: {Interval}", CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);

                signatureCache.CleanupExpiredSignatures();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 正常关闭
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "清理签名缓存时发生错误");
            }
        }

        logger.LogInformation("签名缓存清理服务已停止");
    }
}

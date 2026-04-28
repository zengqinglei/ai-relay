using AiRelay.Application.UsageRecords.AppServices;
using AiRelay.Application.UsageRecords.Dtos.Lifecycle;
using AiRelay.Application.UsageRecords.Queue;
using System.Threading.Channels;

namespace AiRelay.Api.HostedServices.Workers;

/// <summary>
/// 账户使用记录后台服务（使用 Channel 实现生产者-消费者模式）
/// </summary>
public class AccountUsageRecordWorker(
    IServiceProvider serviceProvider,
    ILogger<AccountUsageRecordWorker> logger) : BackgroundService, IUsageRecordQueue
{
    private readonly Channel<IUsageRecordItem> _channel = Channel.CreateUnbounded<IUsageRecordItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>
    /// 添加使用记录到队列（非阻塞）
    /// </summary>
    public bool TryEnqueue(IUsageRecordItem item)
    {
        return _channel.Writer.TryWrite(item);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("账户使用记录后台服务已启动");

        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var usageLifecycleAppService = scope.ServiceProvider.GetRequiredService<IUsageLifecycleAppService>();

                switch (item)
                {
                    case UsageRecordStartItem start:
                        logger.LogDebug("接收到 UsageRecordStartItem: UsageRecordId={UsageRecordId}", start.UsageRecordId);
                        await usageLifecycleAppService.StartUsageAsync(
                            new StartUsageInputDto(
                                start.UsageRecordId,
                                start.UserId,
                                start.Source,
                                start.CorrelationId,
                                start.SessionId,
                                start.ApiKeyId,
                                start.ApiKeyName,
                                start.IsStreaming,
                                start.DownRequestMethod,
                                start.DownRequestUrl,
                                start.DownModelId,
                                start.DownClientIp,
                                start.DownUserAgent,
                                start.DownRequestHeaders,
                                start.DownRequestBody));
                        break;

                    case UsageRecordAttemptStartItem attemptStart:
                        logger.LogDebug("接收到 UsageRecordAttemptStartItem: UsageRecordId={UsageRecordId}, Attempt={Attempt}",
                            attemptStart.UsageRecordId, attemptStart.AttemptNumber);
                        await usageLifecycleAppService.StartAttemptAsync(
                            new StartAttemptInputDto(
                                attemptStart.UsageRecordId,
                                attemptStart.AttemptNumber,
                                attemptStart.AccountTokenId,
                                attemptStart.AccountTokenName,
                                attemptStart.Provider,
                                attemptStart.AuthMethod,
                                attemptStart.ProviderGroupId,
                                attemptStart.ProviderGroupName,
                                attemptStart.GroupRateMultiplier,
                                attemptStart.UpModelId,
                                attemptStart.UpUserAgent,
                                attemptStart.UpRequestUrl,
                                attemptStart.UpRequestHeaders,
                                attemptStart.UpRequestBody));
                        break;

                    case UsageRecordAttemptEndItem attemptEnd:
                        logger.LogDebug("接收到 UsageRecordAttemptEndItem: UsageRecordId={UsageRecordId}, Attempt={Attempt}, Status={Status}",
                            attemptEnd.UsageRecordId, attemptEnd.AttemptNumber, attemptEnd.Status);
                        await usageLifecycleAppService.CompleteAttemptAsync(
                            new CompleteAttemptInputDto(
                                attemptEnd.UsageRecordId,
                                attemptEnd.AttemptNumber,
                                attemptEnd.UpStatusCode,
                                attemptEnd.DurationMs,
                                attemptEnd.Status,
                                attemptEnd.StatusDescription,
                                attemptEnd.UpResponseBody,
                                attemptEnd.UpRequestHeaders,
                                attemptEnd.UpRequestBody));
                        break;

                    case UsageRecordEndItem end:
                        logger.LogDebug("接收到 UsageRecordEndItem: UsageRecordId={UsageRecordId}, FinalStatus={Status}, AttemptCount={Count}",
                            end.UsageRecordId, end.Status, end.AttemptCount);
                        await usageLifecycleAppService.FinishUsageAsync(
                            new FinishUsageInputDto(
                                end.UsageRecordId,
                                end.Duration,
                                end.Status,
                                end.StatusDescription,
                                end.DownResponseBody,
                                end.InputTokens,
                                end.OutputTokens,
                                end.CacheReadTokens,
                                end.CacheCreationTokens,
                                end.AttemptCount,
                                end.DownStatusCode,
                                end.DownRequestHeaders,
                                end.DownRequestBody));
                        break;

                    default:
                        logger.LogWarning("未知的使用记录类型: {Type}", item.GetType().Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "处理使用记录严重失败! UsageRecordId={UsageRecordId}, Type={Type}, Message={Message}",
                    item.UsageRecordId, item.GetType().Name, ex.Message);
            }
        }

        logger.LogInformation("账户使用记录后台服务已停止");
    }
}


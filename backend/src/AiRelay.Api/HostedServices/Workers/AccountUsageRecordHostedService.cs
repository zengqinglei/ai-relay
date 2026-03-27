using AiRelay.Application.UsageRecords.AppServices;
using AiRelay.Application.UsageRecords.Dtos.Lifecycle;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using System.Threading.Channels;

namespace AiRelay.Api.HostedServices.Workers;

/// <summary>
/// 账户使用记录后台服务（使用 Channel 实现生产者-消费者模式）
/// </summary>
public class AccountUsageRecordHostedService(
    IServiceProvider serviceProvider,
    ILogger<AccountUsageRecordHostedService> logger) : BackgroundService
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
                        await usageLifecycleAppService.StartUsageAsync(
                            new StartUsageInputDto(
                                start.UsageRecordId,
                                start.CorrelationId,
                                start.Platform,
                                start.ApiKeyId,
                                start.ApiKeyName,
                                start.IsStreaming,
                                start.DownRequestMethod,
                                start.DownRequestUrl,
                                start.DownModelId,
                                start.DownClientIp,
                                start.DownUserAgent,
                                start.DownRequestHeaders,
                                start.DownRequestBody
                            ));
                        break;

                    case UsageRecordAttemptStartItem attemptStart:
                        await usageLifecycleAppService.StartAttemptAsync(
                            new StartAttemptInputDto(
                                attemptStart.UsageRecordId,
                                attemptStart.AttemptNumber,
                                attemptStart.AccountTokenId,
                                attemptStart.AccountTokenName,
                                attemptStart.ProviderGroupId,
                                attemptStart.ProviderGroupName,
                                attemptStart.GroupRateMultiplier,
                                attemptStart.UpModelId,
                                attemptStart.UpUserAgent,
                                attemptStart.UpRequestUrl,
                                attemptStart.UpRequestHeaders,
                                attemptStart.UpRequestBody
                            ));
                        break;

                    case UsageRecordAttemptEndItem attemptEnd:
                        await usageLifecycleAppService.CompleteAttemptAsync(
                            new CompleteAttemptInputDto(
                                attemptEnd.UsageRecordId,
                                attemptEnd.AttemptNumber,
                                attemptEnd.UpStatusCode,
                                attemptEnd.DurationMs,
                                attemptEnd.Status,
                                attemptEnd.StatusDescription,
                                attemptEnd.UpResponseBody
                            ));
                        break;

                    case UsageRecordEndItem end:
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
                                end.AttemptCount
                            ));
                        break;

                    default:
                        logger.LogWarning("未知的使用记录类型: {Type}", item.GetType().Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "处理账户使用记录失败: UsageRecordId={UsageRecordId}, Type={Type}",
                    item.UsageRecordId, item.GetType().Name);
            }
        }
        logger.LogInformation("账户使用记录后台服务已停止");
    }
}

/// <summary>
/// 使用记录项基础接口
/// </summary>
public interface IUsageRecordItem
{
    Guid UsageRecordId { get; }
}

/// <summary>
/// 使用记录开始项（INSERT UsageRecord，Status=InProgress）
/// </summary>
public record UsageRecordStartItem(
    Guid UsageRecordId,
    string CorrelationId,
    ProviderPlatform Platform,
    Guid ApiKeyId,
    string ApiKeyName,
    bool IsStreaming,
    string DownRequestMethod,
    string DownRequestUrl,
    string? DownModelId,
    string? DownClientIp,
    string? DownUserAgent,
    string? DownRequestHeaders,
    string? DownRequestBody
) : IUsageRecordItem;

/// <summary>
/// 单次上游尝试开始项（INSERT UsageRecordAttempt，Status=InProgress，选号后立即入队）
/// </summary>
public record UsageRecordAttemptStartItem(
    Guid UsageRecordId,
    int AttemptNumber,
    Guid AccountTokenId,
    string AccountTokenName,
    Guid? ProviderGroupId,
    string? ProviderGroupName,
    decimal? GroupRateMultiplier,
    string? UpModelId,
    string? UpUserAgent,
    string? UpRequestUrl,
    string? UpRequestHeaders,
    string? UpRequestBody
) : IUsageRecordItem;

/// <summary>
/// 单次上游尝试结束项（UPDATE UsageRecordAttempt 为最终状态，HTTP 请求结束后入队）
/// </summary>
public record UsageRecordAttemptEndItem(
    Guid UsageRecordId,
    int AttemptNumber,
    int? UpStatusCode,
    long DurationMs,
    UsageStatus Status,
    string? StatusDescription,
    string? UpResponseBody
) : IUsageRecordItem;

/// <summary>
/// 使用记录结束项（UPDATE UsageRecord 为最终状态）
/// </summary>
public record UsageRecordEndItem(
    Guid UsageRecordId,
    long Duration,
    UsageStatus Status,
    string? StatusDescription,
    string? DownResponseBody,
    int? InputTokens,
    int? OutputTokens,
    int? CacheReadTokens,
    int? CacheCreationTokens,
    int AttemptCount
) : IUsageRecordItem;

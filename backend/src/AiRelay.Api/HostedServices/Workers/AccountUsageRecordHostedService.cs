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
                                start.ProviderGroupId,
                                start.ProviderGroupName,
                                start.GroupRateMultiplier,
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

                    case UsageRecordAttemptItem attemptItem:
                        await usageLifecycleAppService.AddAttemptAsync(
                            new AddAttemptInputDto(
                                attemptItem.UsageRecordId,
                                attemptItem.AttemptNumber,
                                attemptItem.AccountTokenId,
                                attemptItem.AccountTokenName,
                                attemptItem.UpModelId,
                                attemptItem.UpUserAgent,
                                attemptItem.UpRequestUrl,
                                attemptItem.UpRequestHeaders,
                                attemptItem.UpRequestBody,
                                attemptItem.UpResponseBody,
                                attemptItem.UpStatusCode,
                                attemptItem.DurationMs,
                                attemptItem.Status,
                                attemptItem.StatusDescription
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
                                end.AttemptCount,
                                end.UpModelId,
                                end.AccountTokenId
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
    Guid ProviderGroupId,
    string ProviderGroupName,
    decimal GroupRateMultiplier,
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
/// 单次上游尝试记录项（INSERT UsageRecordAttempt）
/// </summary>
public record UsageRecordAttemptItem(
    Guid UsageRecordId,
    int AttemptNumber,
    Guid AccountTokenId,
    string AccountTokenName,
    string? UpModelId,
    string? UpUserAgent,
    string? UpRequestUrl,
    string? UpRequestHeaders,
    string? UpRequestBody,
    string? UpResponseBody,
    int? UpStatusCode,
    long DurationMs,
    UsageStatus Status,
    string? StatusDescription
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
    int AttemptCount,
    // 最终成功尝试的账号信息（用于定价和统计，Platform 从 UsageRecord 已有字段读取）
    string? UpModelId,
    Guid? AccountTokenId
) : IUsageRecordItem;

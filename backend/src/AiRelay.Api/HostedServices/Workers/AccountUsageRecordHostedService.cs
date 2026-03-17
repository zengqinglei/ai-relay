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

                // 使用模式匹配处理不同类型的记录
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
                                start.AccountTokenId,
                                start.AccountTokenName,
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
                                start.DownRequestBody,
                                start.UpModelId,
                                start.UpRequestUrl,
                                start.UpUserAgent,
                                start.UpRequestHeaders,
                                start.UpRequestBody
                            ));
                        break;

                    case UsageRecordEndItem end:
                        await usageLifecycleAppService.FinishUsageAsync(
                            new FinishUsageInputDto(
                                end.UsageRecordId,
                                end.Duration,
                                end.UpStatusCode,
                                end.Status,
                                end.StatusDescription,
                                end.UpResponseBody,
                                end.DownResponseBody,
                                end.InputTokens,
                                end.OutputTokens,
                                end.CacheReadTokens,
                                end.CacheCreationTokens
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
/// 使用记录开始项（包含下游请求信息）
/// </summary>
public record UsageRecordStartItem(
    Guid UsageRecordId,
    string CorrelationId,
    ProviderPlatform Platform,
    Guid ApiKeyId,
    string ApiKeyName,
    Guid AccountTokenId,
    string AccountTokenName,
    Guid ProviderGroupId,
    string ProviderGroupName,
    decimal GroupRateMultiplier,
    bool IsStreaming,
    string DownRequestMethod,
    string DownRequestUrl,
    string? DownModelId,
    string? DownClientIp,
    string? DownUserAgent,
    string? UpModelId,
    string? UpUserAgent,
    string? UpRequestUrl,
    string? DownRequestHeaders,
    string? UpRequestHeaders
) : IUsageRecordItem
{
    public string? DownRequestBody { get; private set; }
    public string? UpRequestBody { get; private set; }

    public void LoggingBody(string downRequestBody, string upRequestBody)
    {
        DownRequestBody = downRequestBody;
        UpRequestBody = upRequestBody;
    }
}

/// <summary>
/// 使用记录结束项（包含上游响应信息和 Token 统计）
/// </summary>
public record UsageRecordEndItem(
    Guid UsageRecordId,
    long Duration,
    int? UpStatusCode,
    UsageStatus Status,
    string? StatusDescription,
    string? UpResponseBody,
    string? DownResponseBody,
    int? InputTokens,
    int? OutputTokens,
    int? CacheReadTokens,
    int? CacheCreationTokens
) : IUsageRecordItem;

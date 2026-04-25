using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.Entities;
using AiRelay.Domain.UsageRecords.Providers;
using Leistd.Ddd.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.UsageRecords.DomainServices;

public class UsageRecordDomainService(
    IPricingProvider pricingProvider,
    IRepository<UsageRecord, Guid> usageRepository,
    IRepository<UsageRecordDetail, Guid> detailRepository,
    IRepository<UsageRecordAttempt, Guid> attemptRepository,
    IRepository<UsageRecordAttemptDetail, Guid> attemptDetailRepository,
    IQueryableAsyncExecuter asyncExecuter,
    ILogger<UsageRecordDomainService> logger)
{
    public async Task ProcessCompletionAsync(
        UsageRecord record,
        decimal groupRateMultiplier,
        long duration,
        UsageStatus status,
        string? statusDescription,
        string? downResponseBody,
        int? inputTokens,
        int? outputTokens,
        int? cacheReadTokens,
        int? cacheCreationTokens,
        int attemptCount,
        int? downStatusCode,
        string? upModelId,
        string? downRequestHeaders = null,
        string? downRequestBody = null,
        CancellationToken cancellationToken = default)
    {
        decimal baseCost = 0;
        if (!string.IsNullOrEmpty(upModelId))
        {
            var pricing = await pricingProvider.GetPricingAsync(upModelId, cancellationToken);
            if (pricing != null)
            {
                var input = inputTokens ?? 0;
                var output = outputTokens ?? 0;
                var cacheRead = cacheReadTokens ?? 0;
                var cacheCreation = cacheCreationTokens ?? 0;

                var inputCost = input * pricing.InputPrice;
                var outputCost = output * pricing.OutputPrice;
                var cacheReadCost = cacheRead * pricing.CacheReadPrice;
                var cacheCreationCost = cacheCreation * pricing.CacheCreationPrice;

                baseCost = inputCost + outputCost + cacheReadCost + cacheCreationCost;

                if (pricing.LongContextInputThreshold.HasValue &&
                    input > pricing.LongContextInputThreshold.Value)
                {
                    baseCost = baseCost - inputCost - outputCost
                             + (inputCost * (pricing.LongContextInputMultiplier ?? 1))
                             + (outputCost * (pricing.LongContextOutputMultiplier ?? 1));
                }
            }
        }

        record.Complete(
            groupRateMultiplier,
            duration,
            status,
            statusDescription,
            downResponseBody,
            inputTokens,
            outputTokens,
            cacheReadTokens,
            cacheCreationTokens,
            baseCost,
            attemptCount,
            downStatusCode,
            downRequestHeaders,
            downRequestBody);

        await usageRepository.UpdateAsync(record, cancellationToken);
    }

    /// <summary>
    /// 清理过期的使用记录数据
    /// </summary>
    /// <param name="detailRetentionDays">详情数据保留天数</param>
    /// <param name="summaryRetentionDays">摘要数据保留天数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>(清理的详情记录数, 清理的摘要记录数)</returns>
    public async Task<(int DeletedDetails, int DeletedSummaries)> CleanupExpiredRecordsAsync(
        int detailRetentionDays,
        int summaryRetentionDays,
        CancellationToken cancellationToken = default)
    {
        var detailCutoff = DateTime.UtcNow.AddDays(-detailRetentionDays);
        var summaryCutoff = DateTime.UtcNow.AddDays(-summaryRetentionDays);

        var usageQuery = await usageRepository.GetQueryableAsync(cancellationToken);
        var attemptQuery = await attemptRepository.GetQueryableAsync(cancellationToken);
        var detailQuery = await detailRepository.GetQueryableAsync(cancellationToken);
        var attemptDetailQuery = await attemptDetailRepository.GetQueryableAsync(cancellationToken);

        var expiredRecordIds = await asyncExecuter.ToListAsync(
            usageQuery
                .Where(r => r.CreationTime < detailCutoff)
                .Select(r => r.Id),
            cancellationToken);

        var expiredAttemptIds = await asyncExecuter.ToListAsync(
            attemptQuery
                .Where(a => a.StartTime < detailCutoff)
                .Select(a => a.Id),
            cancellationToken);

        var deletedUsageDetailCount = expiredRecordIds.Count == 0
            ? 0
            : await asyncExecuter.CountAsync(
                detailQuery.Where(d => expiredRecordIds.Contains(d.UsageRecordId)),
                cancellationToken);

        var deletedAttemptDetailCount = expiredAttemptIds.Count == 0
            ? 0
            : await asyncExecuter.CountAsync(
                attemptDetailQuery.Where(d => expiredAttemptIds.Contains(d.UsageRecordAttemptId)),
                cancellationToken);

        if (expiredRecordIds.Count > 0)
        {
            await detailRepository.DeleteManyAsync(d => expiredRecordIds.Contains(d.UsageRecordId), cancellationToken);
        }

        if (expiredAttemptIds.Count > 0)
        {
            await attemptDetailRepository.DeleteManyAsync(d => expiredAttemptIds.Contains(d.UsageRecordAttemptId), cancellationToken);
        }

        logger.LogDebug("已清理 Body 详情（过期阈值: {DetailCutoff:yyyy-MM-dd}）", detailCutoff);

        var deletedAttemptSummaryCount = await asyncExecuter.CountAsync(
            attemptQuery.Where(a => a.StartTime < summaryCutoff),
            cancellationToken);

        var deletedUsageSummaryCount = await asyncExecuter.CountAsync(
            usageQuery.Where(r => r.CreationTime < summaryCutoff),
            cancellationToken);

        if (deletedAttemptSummaryCount > 0)
        {
            await attemptRepository.DeleteManyAsync(a => a.StartTime < summaryCutoff, cancellationToken);
        }

        if (deletedUsageSummaryCount > 0)
        {
            await usageRepository.DeleteManyAsync(r => r.CreationTime < summaryCutoff, cancellationToken);
        }

        logger.LogInformation(
            "清理完成：详情截止 {DetailCutoff:yyyy-MM-dd}，摘要截止 {SummaryCutoff:yyyy-MM-dd}，删除详情 {DeletedDetails} 条，删除摘要 {DeletedSummaries} 条",
            detailCutoff,
            summaryCutoff,
            deletedUsageDetailCount + deletedAttemptDetailCount,
            deletedUsageSummaryCount + deletedAttemptSummaryCount);

        return (
            deletedUsageDetailCount + deletedAttemptDetailCount,
            deletedUsageSummaryCount + deletedAttemptSummaryCount);
    }
}

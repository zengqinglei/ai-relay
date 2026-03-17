using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.Entities;
using AiRelay.Domain.UsageRecords.Providers;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.UsageRecords.DomainServices;

public class UsageRecordDomainService(
    IPricingProvider pricingProvider,
    IRepository<UsageRecord, Guid> usageRepository,
    AccountUsageCacheDomainService usageCacheService)
{
    public async Task ProcessCompletionAsync(
        UsageRecord record,
        long duration,
        int? upStatusCode,
        UsageStatus status,
        string? statusDescription,
        string? upResponseBody,
        string? downResponseBody,
        int? inputTokens,
        int? outputTokens,
        int? cacheReadTokens,
        int? cacheCreationTokens,
        CancellationToken cancellationToken = default)
    {
        // 1. 获取基础价格
        decimal baseCost = 0;
        if (!string.IsNullOrEmpty(record.UpModelId))
        {
            var pricing = await pricingProvider.GetPricingAsync(record.UpModelId, cancellationToken);
            if (pricing != null)
            {
                var input = inputTokens ?? 0;
                var output = outputTokens ?? 0;
                var cacheRead = cacheReadTokens ?? 0;
                var cacheCreation = cacheCreationTokens ?? 0;

                // 计算基础成本
                var inputCost = input * pricing.InputPrice;
                var outputCost = output * pricing.OutputPrice;
                var cacheReadCost = cacheRead * pricing.CacheReadPrice;
                var cacheCreationCost = cacheCreation * pricing.CacheCreationPrice;

                baseCost = inputCost + outputCost + cacheReadCost + cacheCreationCost;

                // 应用长上下文倍率
                if (pricing.LongContextInputThreshold.HasValue &&
                    input > pricing.LongContextInputThreshold.Value)
                {
                    baseCost = baseCost - inputCost - outputCost
                             + (inputCost * (pricing.LongContextInputMultiplier ?? 1))
                             + (outputCost * (pricing.LongContextOutputMultiplier ?? 1));
                }
            }
        }

        // 2. 更新实体状态（倍率已在记录创建时快照到 GroupRateMultiplier）
        record.Complete(
            duration,
            upStatusCode,
            status,
            statusDescription,
            upResponseBody,
            downResponseBody,
            inputTokens,
            outputTokens,
            cacheReadTokens,
            cacheCreationTokens,
            baseCost);

        // 3. 持久化记录
        await usageRepository.UpdateAsync(record, cancellationToken);

        // 4. 更新缓存统计
        await usageCacheService.IncrementUsageAsync(
                record.AccountTokenId,
                record.Platform,
                record.AccountTokenName,
                cancellationToken);
    }
}

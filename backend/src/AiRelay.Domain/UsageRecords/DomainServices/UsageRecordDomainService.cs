using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.Entities;
using AiRelay.Domain.UsageRecords.Providers;
using Leistd.Ddd.Domain.Repositories;

namespace AiRelay.Domain.UsageRecords.DomainServices;

public class UsageRecordDomainService(
    IPricingProvider pricingProvider,
    IRepository<UsageRecord, Guid> usageRepository)
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
        CancellationToken cancellationToken = default)
    {
        // 1. 获取基础价格
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

        // 2. 更新实体状态
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
            downStatusCode);

        // 3. 持久化记录
        await usageRepository.UpdateAsync(record, cancellationToken);

    }
}

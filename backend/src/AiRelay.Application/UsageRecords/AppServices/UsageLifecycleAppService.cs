using AiRelay.Application.UsageRecords.Dtos.Lifecycle;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.UsageRecords.DomainServices;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.Lock.Core;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.UsageRecords.AppServices;

/// <summary>
/// 使用记录生命周期应用服务
/// </summary>
public class UsageLifecycleAppService(
    IRepository<UsageRecord, Guid> usageRepository,
    IRepository<AccountToken, Guid> accountTokenRepository,
    IRepository<ApiKey, Guid> apiKeyRepository,
    UsageRecordDomainService usageRecordDomainService,
    IDistributedLock distributedLock,
    IQueryableAsyncExecuter asyncExecuter,
    ILogger<UsageLifecycleAppService> logger) : BaseAppService, IUsageLifecycleAppService
{
    public async Task<StartUsageOutputDto> StartUsageAsync(
        StartUsageInputDto input,
        CancellationToken cancellationToken = default)
    {
        var record = new UsageRecord(
            input.UsageRecordId,
            input.CorrelationId,
            input.Platform,
            input.ApiKeyId,
            input.ApiKeyName,
            input.AccountTokenId,
            input.AccountTokenName,
            input.ProviderGroupId,
            input.ProviderGroupName,
            input.GroupRateMultiplier,
            input.IsStreaming,
            input.DownRequestMethod,
            input.DownRequestUrl,
            input.DownModelId,
            input.DownClientIp,
            input.DownUserAgent,
            input.DownRequestHeaders,
            input.DownRequestBody,
            input.UpModelId,
            input.UpRequestUrl,
            input.UpUserAgent,
            input.UpRequestHeaders,
            input.UpRequestBody);

        await usageRepository.InsertAsync(record, cancellationToken);

        logger.LogDebug(
            "开始记录 Usage: Id={UsageId}, CorrelationId={CorrelationId}, Path={Path}",
            record.Id,
            record.CorrelationId,
            input.DownRequestUrl);

        return new StartUsageOutputDto { UsageRecordId = record.Id };
    }

    public async Task FinishUsageAsync(
        FinishUsageInputDto input,
        CancellationToken cancellationToken = default)
    {
        // 获取 UsageRecord 并包含 Detail
        var query = await usageRepository.GetQueryIncludingAsync(x => x.Detail);
        var record = await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == input.UsageRecordId), cancellationToken);

        if (record == null)
        {
            logger.LogWarning("未找到 Usage 记录: Id={UsageId}", input.UsageRecordId);
            return;
        }

        // 调用 DomainService 完成核心逻辑
        await usageRecordDomainService.ProcessCompletionAsync(
            record,
            input.Duration,
            input.UpStatusCode,
            input.Status,
            input.StatusDescription,
            input.UpResponseBody,
            input.DownResponseBody,
            input.InputTokens,
            input.OutputTokens,
            input.CacheReadTokens,
            input.CacheCreationTokens,
            cancellationToken
        );

        logger.LogDebug(
            "完成 Usage: Id={UsageRecordId}, Tokens={In}/{Out}, Cost={Cost}",
            input.UsageRecordId,
            input.InputTokens,
            input.OutputTokens,
            record.FinalCost);

        // 累加统计到 AccountToken 和 ApiKey
        var tokens = (long)((record.InputTokens ?? 0) + (record.OutputTokens ?? 0) + (record.CacheReadTokens ?? 0));
        var cost = record.FinalCost ?? 0m;
        var isSuccess = record.Status == UsageStatus.Success;

        await AccumulateAccountTokenStatsAsync(record.AccountTokenId, tokens, cost, isSuccess, cancellationToken);
        await AccumulateApiKeyStatsAsync(record.ApiKeyId, tokens, cost, isSuccess, cancellationToken);
    }

    private async Task AccumulateAccountTokenStatsAsync(Guid accountTokenId, long tokens, decimal cost, bool isSuccess, CancellationToken cancellationToken)
    {
        await using var handle = await distributedLock.LockAsync($"stats:account:{accountTokenId}", cancellationToken);
        var account = await accountTokenRepository.GetByIdAsync(accountTokenId, cancellationToken);
        if (account == null) return;
        account.AccumulateStats(tokens, cost, isSuccess);
        await accountTokenRepository.UpdateAsync(account, cancellationToken: cancellationToken);
    }

    private async Task AccumulateApiKeyStatsAsync(Guid apiKeyId, long tokens, decimal cost, bool isSuccess, CancellationToken cancellationToken)
    {
        await using var handle = await distributedLock.LockAsync($"stats:apikey:{apiKeyId}", cancellationToken);
        var apiKey = await apiKeyRepository.GetByIdAsync(apiKeyId, cancellationToken);
        if (apiKey == null) return;
        apiKey.AccumulateStats(tokens, cost, isSuccess);
        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken: cancellationToken);
    }
}

using AiRelay.Application.UsageRecords.Dtos.Lifecycle;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.DomainServices;
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
    IRepository<UsageRecordAttempt, Guid> attemptRepository,
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
            input.SessionId,
            input.ApiKeyId,
            input.ApiKeyName,
            input.IsStreaming,
            input.DownRequestMethod,
            input.DownRequestUrl,
            input.DownModelId,
            input.DownClientIp,
            input.DownUserAgent,
            input.DownRequestHeaders,
            input.DownRequestBody);

        await usageRepository.InsertAsync(record, cancellationToken);

        logger.LogDebug(
            "开始记录 Usage: Id={UsageId}, CorrelationId={CorrelationId}, Path={Path}",
            record.Id,
            record.CorrelationId,
            input.DownRequestUrl);

        return new StartUsageOutputDto { UsageRecordId = record.Id };
    }

    public async Task StartAttemptAsync(
        StartAttemptInputDto input,
        CancellationToken cancellationToken = default)
    {
        var attempt = new UsageRecordAttempt(
            input.UsageRecordId,
            input.AttemptNumber,
            input.AccountTokenId,
            input.AccountTokenName,
            input.Provider,
            input.AuthMethod,
            input.ProviderGroupId,
            input.ProviderGroupName,
            input.GroupRateMultiplier,
            input.UpModelId,
            input.UpUserAgent,
            input.UpRequestUrl,
            input.UpRequestHeaders,
            input.UpRequestBody);

        await attemptRepository.InsertAsync(attempt, cancellationToken);

        logger.LogDebug(
            "开始 UsageRecordAttempt: UsageId={UsageId}, Attempt={AttemptNumber}, Account={AccountName}",
            input.UsageRecordId,
            input.AttemptNumber,
            input.AccountTokenName);
    }

    public async Task CompleteAttemptAsync(
        CompleteAttemptInputDto input,
        CancellationToken cancellationToken = default)
    {
        var query = await attemptRepository.GetQueryIncludingAsync(cancellationToken, (UsageRecordAttempt a) => a.Detail);
        var attempt = await asyncExecuter.FirstOrDefaultAsync(
            query.Where(a => a.UsageRecordId == input.UsageRecordId && a.AttemptNumber == input.AttemptNumber),
            cancellationToken);

        if (attempt == null)
        {
            logger.LogWarning("未找到 UsageRecordAttempt: UsageId={UsageId}, Attempt={AttemptNumber}",
                input.UsageRecordId, input.AttemptNumber);
            return;
        }

        attempt.Complete(input.UpStatusCode, input.DurationMs, input.Status, input.StatusDescription, input.UpResponseBody);
        await attemptRepository.UpdateAsync(attempt, cancellationToken: cancellationToken);

        // 累加账号调用次数统计
        await AccumulateAccountTokenCallStatsAsync(attempt.AccountTokenId, input.Status == UsageStatus.Success, cancellationToken);

        logger.LogDebug(
            "完成 UsageRecordAttempt: UsageId={UsageId}, Attempt={AttemptNumber}, Status={Status}",
            input.UsageRecordId,
            input.AttemptNumber,
            input.Status);
    }

    public async Task FinishUsageAsync(
        FinishUsageInputDto input,
        CancellationToken cancellationToken = default)
    {
        var query = await usageRepository.GetQueryIncludingAsync(cancellationToken, x => x.Detail);
        var record = await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == input.UsageRecordId), cancellationToken);

        if (record == null)
        {
            logger.LogWarning("未找到 Usage 记录: Id={UsageId}", input.UsageRecordId);
            return;
        }

        // 从最后一次 Attempt 取 group/account/upModel 信息（用于定价和缓存统计）
        var attemptQuery = await attemptRepository.GetQueryableAsync(cancellationToken);
        var lastAttempt = await asyncExecuter.FirstOrDefaultAsync(
            attemptQuery
                .Where(a => a.UsageRecordId == input.UsageRecordId)
                .OrderByDescending(a => a.AttemptNumber),
            cancellationToken);

        var groupRateMultiplier = lastAttempt?.GroupRateMultiplier ?? 1m;
        var upModelId = lastAttempt?.UpModelId;
        var accountTokenId = lastAttempt?.AccountTokenId;

        await usageRecordDomainService.ProcessCompletionAsync(
            record,
            groupRateMultiplier,
            input.Duration,
            input.Status,
            input.StatusDescription,
            input.DownResponseBody,
            input.InputTokens,
            input.OutputTokens,
            input.CacheReadTokens,
            input.CacheCreationTokens,
            input.AttemptCount,
            input.DownStatusCode,
            upModelId,
            cancellationToken);

        logger.LogDebug(
            "完成 Usage: Id={UsageRecordId}, Tokens={In}/{Out}, Cost={Cost}",
            input.UsageRecordId,
            input.InputTokens,
            input.OutputTokens,
            record.FinalCost);

        // 累加统计到 ApiKey
        var tokens = (long)((record.InputTokens ?? 0) + (record.OutputTokens ?? 0) + (record.CacheReadTokens ?? 0));
        var cost = record.FinalCost ?? 0m;
        var isSuccess = record.Status == UsageStatus.Success;

        await AccumulateApiKeyStatsAsync(record.ApiKeyId, tokens, cost, isSuccess, cancellationToken);

        // 若有最终账号，累加 token/cost 统计
        if (accountTokenId.HasValue)
        {
            await AccumulateAccountTokenCostStatsAsync(accountTokenId.Value, tokens, cost, cancellationToken);
        }
    }

    private async Task AccumulateAccountTokenCallStatsAsync(Guid accountTokenId, bool isSuccess, CancellationToken cancellationToken)
    {
        await using var handle = await distributedLock.LockAsync($"stats:account:{accountTokenId}", cancellationToken);
        var account = await accountTokenRepository.GetByIdAsync(accountTokenId, cancellationToken);
        if (account == null) return;
        account.AccumulateCallStats(isSuccess);
        await accountTokenRepository.UpdateAsync(account, cancellationToken: cancellationToken);
    }

    private async Task AccumulateAccountTokenCostStatsAsync(Guid accountTokenId, long tokens, decimal cost, CancellationToken cancellationToken)
    {
        await using var handle = await distributedLock.LockAsync($"stats:account:{accountTokenId}", cancellationToken);
        var account = await accountTokenRepository.GetByIdAsync(accountTokenId, cancellationToken);
        if (account == null) return;
        account.AccumulateCostStats(tokens, cost);
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

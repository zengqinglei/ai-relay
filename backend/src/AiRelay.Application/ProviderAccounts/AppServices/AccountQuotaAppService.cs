using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiRelay.Application.ProviderAccounts.AppServices;

/// <summary>
/// 账号配额应用服务实现
/// </summary>
public class AccountQuotaAppService(
    IRepository<AccountToken, Guid> accountTokenRepository,
    IQueryableAsyncExecuter asyncExecuter,
    IDistributedCache cache,
    IChatModelHandlerFactory clientFactory,
    ILogger<AccountQuotaAppService> logger) : BaseAppService, IAccountQuotaAppService
{
    private const int CACHE_EXPIRATION_MINUTES = 10;

    public async Task RefreshAllQuotasAsync(CancellationToken cancellationToken = default)
    {
        // 仅刷新支持配额查询的平台
        var accountQuery = await accountTokenRepository.GetQueryableAsync();
        accountQuery = accountQuery.Where(a => (a.Platform == ProviderPlatform.ANTIGRAVITY || a.Platform == ProviderPlatform.GEMINI_OAUTH) && a.IsActive);
        var accountTokens = await asyncExecuter.ToListAsync(accountQuery, cancellationToken);

        logger.LogInformation("开始刷新 {Count} 个账户的配额信息", accountTokens.Count());

        int successCount = 0, failureCount = 0;

        foreach (var accountToken in accountTokens)
        {
            try
            {
                var client = clientFactory.CreateHandler(accountToken.Platform, accountToken.AccessToken!, accountToken.BaseUrl, accountToken.ExtraProperties);
                var quotaInfos = await client.FetchQuotaAsync(cancellationToken);

                if (quotaInfos == null)
                {
                    continue;
                }
                foreach (var quotaInfo in quotaInfos)
                {
                    var cacheKey = $"account:quota:{accountToken.Id}:{quotaInfo.ModelId}";
                    await cache.SetStringAsync(
                        cacheKey,
                        JsonSerializer.Serialize(quotaInfos),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES)
                        },
                        cancellationToken);
                    logger.LogDebug(
                        "账户 {AccountName} ({Platform}) 配额详情: Model={Model}, Remaining={Remaining}, Tier={Tier}",
                        accountToken.Name,
                        accountToken.Platform,
                        quotaInfo.ModelId,
                        quotaInfo.RemainingQuota,
                        quotaInfo.SubscriptionTier);
                }

                successCount++;
            }
            catch (Exception ex)
            {
                failureCount++;
                logger.LogError(ex, "刷新账户 {AccountName} ({Platform}) 配额时发生错误", accountToken.Name, accountToken.Platform);
            }
        }

        logger.LogInformation(
            "配额刷新完成: 成功={Success}, 失败={Failure}, 总计={Total}",
            successCount,
            failureCount,
            accountTokens.Count());
    }
}

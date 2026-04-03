using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.Extensions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using AiRelay.Domain.Shared.OAuth.Authorize;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.Lock.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账户令牌领域服务
/// </summary>
public class AccountTokenDomainService(
    IServiceProvider serviceProvider,
    IChatModelHandlerFactory chatModelHandlerFactory,
    IRepository<AccountToken, Guid> accountTokenRepository,
    ILock distributedLock,
    IDistributedCache cache,
    IModelProvider modelProvider,
    ILogger<AccountTokenDomainService> logger)
{
    private static readonly TimeSpan RefreshLockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefreshLockWait = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 创建并准备账户
    /// </summary>
    public async Task<AccountToken> CreateAndPrepareAsync(
        ProviderPlatform platform,
        string name,
        Dictionary<string, string>? extraProperties = null,
        string? accessToken = null,
        string? refreshToken = null,
        long? expiresIn = null,
        string? baseUrl = null,
        string? description = null,
        int? maxConcurrency = null,
        List<string>? modelWhites = null,
        Dictionary<string, string>? modelMapping = null,
        bool allowOfficialClientMimic = false,
        bool isCheckStreamHealth = false,
        CancellationToken cancellationToken = default)
    {
        var accountToken = new AccountToken(
            platform,
            name,
            maxConcurrency ?? 10,
            accessToken,
            refreshToken,
            expiresIn,
            baseUrl,
            description,
            extraProperties,
            modelWhites,
            modelMapping,
            allowOfficialClientMimic,
            isCheckStreamHealth);

        // 1. 刷新 Token (针对 Account 类型)
        if (!accountToken.Platform.IsApiKeyPlatform())
        {
            try
            {
                await RefreshTokenIfNeededAsync(accountToken, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "准备账户时刷新 Token 失败: {Name}", accountToken.Name);
            }
        }

        // 2. 获取/验证 Project ID (针对 Gemini OAuth 和 Antigravity)
        var projectId = accountToken.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : null;

        if ((accountToken.Platform == ProviderPlatform.GEMINI_OAUTH || accountToken.Platform == ProviderPlatform.ANTIGRAVITY) &&
            string.IsNullOrEmpty(projectId))
        {
            var handler = chatModelHandlerFactory.CreateHandler(accountToken.Platform, accountToken.AccessToken!, accountToken.BaseUrl, accountToken.ExtraProperties);
            var result = await handler.ValidateConnectionAsync(cancellationToken);

            if (result.IsSuccess)
            {
                if (!string.IsNullOrEmpty(result.ProjectId))
                {
                    accountToken.ExtraProperties["project_id"] = result.ProjectId;
                    accountToken.Update(accountToken.Name, accountToken.BaseUrl, accountToken.Description, accountToken.MaxConcurrency, accountToken.ExtraProperties);
                    logger.LogInformation("自动获取 Project ID 成功: {ProjectId}", result.ProjectId);
                }
            }
            else
            {
                throw new BadRequestException($"账户验证失败：{result.Error}");
            }
        }
        return await accountTokenRepository.InsertAsync(accountToken, cancellationToken: cancellationToken);
    }


    /// <summary>
    /// 刷新 Token（如果需要），使用分布式锁防止并发竞态
    /// </summary>
    public async Task RefreshTokenIfNeededAsync(AccountToken accountToken, CancellationToken cancellationToken = default)
    {
        // 快速路径：不需要刷新直接返回
        if (!accountToken.IsNeedRefreshToken())
            return;

        var lockKey = $"token:refresh:{accountToken.Id}";

        // 尝试获取分布式锁（超时 30s）
        var lockHandle = await distributedLock.TryLockAsync(lockKey, RefreshLockTimeout, cancellationToken);

        if (lockHandle == null)
        {
            // 锁被占用，说明其他 worker 正在刷新，等待后重读 DB 使用最新 token
            logger.LogDebug("Token 刷新锁被占用，等待后重读 DB: {Name}", accountToken.Name);
            await Task.Delay(RefreshLockWait, cancellationToken);
            var fresh = await accountTokenRepository.GetByIdAsync(accountToken.Id, cancellationToken);
            if (fresh != null)
                accountToken.CopyTokenFrom(fresh);
            return;
        }

        await using (lockHandle)
        {
            // double-check：拿到锁后重读 DB，其他 worker 可能已完成刷新
            var latest = await accountTokenRepository.GetByIdAsync(accountToken.Id, cancellationToken);
            if (latest != null)
                accountToken.CopyTokenFrom(latest);

            if (!accountToken.IsNeedRefreshToken())
            {
                logger.LogDebug("Token 已由其他 worker 刷新，跳过: {Name}", accountToken.Name);
                return;
            }

            // 执行刷新
            var remainingMinutes = accountToken.GetTokenRemainingMinutes();
            logger.LogInformation("开始刷新 Token - Name: {Name}, 剩余分钟数: {Minutes}",
                accountToken.Name, remainingMinutes ?? 0);

            if (string.IsNullOrEmpty(accountToken.RefreshToken))
                throw new BadRequestException($"账户 '{accountToken.Name}' 无可用的 RefreshToken");

            var authProvider = serviceProvider.GetKeyedService<IOAuthProvider>(accountToken.Platform);
            if (authProvider == null)
                throw new NotFoundException($"未找到 {accountToken.Platform} Token 刷新服务");

            var tokenInfo = await authProvider.RefreshTokenAsync(accountToken.RefreshToken, accountToken.Platform, cancellationToken);

            accountToken.UpdateTokens(
                tokenInfo.AccessToken,
                tokenInfo.RefreshToken,
                tokenInfo.ExpiresIn);

            // 更新 ExtraProperties（如 chatgpt_account_id）
            if (tokenInfo.ExtraProperties != null && tokenInfo.ExtraProperties.Count > 0)
            {
                foreach (var kvp in tokenInfo.ExtraProperties)
                    accountToken.ExtraProperties[kvp.Key] = kvp.Value;
                accountToken.Update(accountToken.Name, accountToken.BaseUrl, accountToken.Description, accountToken.MaxConcurrency, accountToken.ExtraProperties);
            }

            await accountTokenRepository.UpdateAsync(accountToken, cancellationToken);
            logger.LogInformation("刷新 Token 成功: {Name}", accountToken.Name);
        }
    }

    /// <summary>
    /// 判断账户是否支持指定模型（选号热路径）
    /// 有白名单则校验白名单；无白名单则用上游模型列表（带缓存）与 baseline 取交集后校验
    /// </summary>
    public async Task<bool> IsModelSupportedAsync(AccountToken account, string requestedModel, CancellationToken ct = default)
    {
        var whitelist = account.ModelWhites;
        if (whitelist != null && whitelist.Count > 0)
        {
            if (whitelist.Contains(requestedModel, StringComparer.OrdinalIgnoreCase)) return true;
            return whitelist.Where(k => k.EndsWith('*'))
                .Any(k => requestedModel.StartsWith(k[..^1], StringComparison.OrdinalIgnoreCase));
        }

        // 无白名单：用上游模型列表（带缓存）与 baseline 取交集后校验
        var baselineModels = modelProvider.GetAvailableModels(account.Platform);
        if (baselineModels == null || baselineModels.Count == 0) return true;

        IReadOnlyList<string>? upstreamModelIds = null;
        try
        {
            upstreamModelIds = await FetchAndCacheUpstreamModelsAsync(account, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IsModelSupportedAsync 上游模型拉取失败，降级用 baseline: AccountId={AccountId}", account.Id);
        }

        IEnumerable<string> effectiveModels;
        if (upstreamModelIds != null && upstreamModelIds.Count > 0)
        {
            var upstreamSet = upstreamModelIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            effectiveModels = baselineModels.Where(m => !m.Value.Contains('*') && upstreamSet.Contains(m.Value)).Select(m => m.Value);
        }
        else
        {
            effectiveModels = baselineModels.Where(m => !m.Value.Contains('*')).Select(m => m.Value);
        }

        var effectiveList = effectiveModels.ToList();
        if (effectiveList.Count == 0) return true;

        if (effectiveList.Any(v => v.Equals(requestedModel, StringComparison.OrdinalIgnoreCase))) return true;
        return baselineModels.Where(m => m.Value.EndsWith('*'))
            .Any(m => requestedModel.StartsWith(m.Value[..^1], StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 解析模型映射规则（供 Processor 共用）
    /// 支持通配符：claude-* 或 claude-*-{version}
    /// </summary>
    public static string? ResolveMapping(string model, Dictionary<string, string> mapping)
    {
        // 1. 精确匹配
        if (mapping.TryGetValue(model, out var exact)) return exact;

        // 2. 通配符匹配（最长模式优先）
        var match = mapping
            .Where(kv => kv.Key.Contains('*') && IsWildcardMatch(model, kv.Key))
            .OrderByDescending(kv => kv.Key.Length)
            .FirstOrDefault();

        if (match.Key == null) return null;

        // 3. value 也以 * 结尾 → 前缀替换
        if (match.Value.EndsWith('*'))
        {
            var suffix = model[(match.Key.Length - 1)..];
            return match.Value[..^1] + suffix;
        }

        return match.Value;
    }

    private static bool IsWildcardMatch(string text, string pattern)
    {
        var parts = pattern.Split('*');
        var pos = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part)) continue;

            var idx = text.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            // 第一个部分必须从开头匹配
            if (i == 0 && idx != 0) return false;

            pos = idx + part.Length;
        }

        // 最后一个部分必须到结尾
        if (!string.IsNullOrEmpty(parts[^1]) && pos != text.Length) return false;

        return true;
    }

    private static readonly TimeSpan MinTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxTtl = TimeSpan.FromMinutes(60);

    /// <summary>
    /// 获取上游模型ID集合（仅读缓存，不触发网络请求）
    /// </summary>
    public async Task<HashSet<string>?> GetCachedModelIdsAsync(Guid accountId, CancellationToken ct = default)
    {
        var cached = await cache.GetStringAsync(CacheKey(accountId), ct);
        if (string.IsNullOrEmpty(cached)) return null;
        var models = JsonSerializer.Deserialize<List<string>>(cached);
        return models?.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 拉取上游模型列表并写入缓存（供 UI 展示调用，允许 IO）
    /// </summary>
    public async Task<IReadOnlyList<string>?> FetchAndCacheUpstreamModelsAsync(AccountToken account, CancellationToken ct = default)
    {
        try
        {
            var handler = chatModelHandlerFactory.CreateHandler(
                account.Platform,
                account.AccessToken!,
                account.BaseUrl,
                account.ExtraProperties,
                shouldMimicOfficialClient: true,
                modelWhites: account.ModelWhites,
                modelMapping: account.ModelMapping);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var upstreamModels = await handler.GetModelsAsync(cts.Token);
            if (upstreamModels == null || upstreamModels.Count == 0) return null;

            var ids = upstreamModels.Select(m => m.Value).ToList();

            var ttl = MinTtl + TimeSpan.FromMinutes(Random.Shared.NextDouble() * (MaxTtl - MinTtl).TotalMinutes);
            await cache.SetStringAsync(
                CacheKey(account.Id),
                JsonSerializer.Serialize(ids),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                ct);

            logger.LogInformation("上游模型缓存写入: AccountId={AccountId}, Count={Count}, TTL={TTL}min",
                account.Id, ids.Count, (int)ttl.TotalMinutes);
            return ids;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "上游模型拉取失败: AccountId={AccountId}", account.Id);
            return null;
        }
    }

    private static string CacheKey(Guid accountId) => $"account:upstream-models:{accountId}";
}

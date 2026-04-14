using AiRelay.Domain.ProviderAccounts.Entities;

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
        Provider provider,
        AuthMethod authMethod,
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
            provider,
            authMethod,
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

        // 1. 刷新 Token (针对 OAuth 类型)
        if (accountToken.AuthMethod == AuthMethod.OAuth)
        {
            try
            {
                await RefreshTokenIfNeededAsync(accountToken, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "准备账户时刷新 Token 失败: {Name}({Provider}-{AuthMethod})", 
                    accountToken.Name, accountToken.Provider, accountToken.AuthMethod);
            }
        }

        // 2. 获取/验证 Project ID (针对 Gemini OAuth 和 Antigravity)
        var projectId = accountToken.ExtraProperties.TryGetValue("project_id", out var pid) ? pid : null;

        if (((accountToken.Provider == Provider.Gemini && accountToken.AuthMethod == AuthMethod.OAuth) || accountToken.Provider == Provider.Antigravity) &&
            string.IsNullOrEmpty(projectId))
        {
            var handler = chatModelHandlerFactory.CreateHandler(accountToken.Provider, accountToken.AuthMethod, accountToken.AccessToken!, accountToken.BaseUrl, accountToken.ExtraProperties);
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
            logger.LogDebug("Token 刷新锁被占用，等待后重读 DB: {Name}({Provider}-{AuthMethod})", 
                accountToken.Name, accountToken.Provider, accountToken.AuthMethod);
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
                logger.LogDebug("Token 已由其他 worker 刷新，跳过: {Name}({Provider}-{AuthMethod})", 
                    accountToken.Name, accountToken.Provider, accountToken.AuthMethod);
                return;
            }

            // 执行刷新
            var remainingMinutes = accountToken.GetTokenRemainingMinutes();
            logger.LogInformation("开始刷新 Token - Name: {Name}({Provider}-{AuthMethod}), 剩余分钟数: {Minutes}",
                accountToken.Name, accountToken.Provider, accountToken.AuthMethod, remainingMinutes ?? 0);

            if (string.IsNullOrEmpty(accountToken.RefreshToken))
                throw new BadRequestException($"账户 '{accountToken.Name}' 无可用的 RefreshToken");

            var authProvider = serviceProvider.GetKeyedService<IOAuthProvider>(accountToken.Provider);
            if (authProvider == null)
                throw new NotFoundException($"未找到 {accountToken.Provider} Token 刷新服务");

            var tokenInfo = await authProvider.RefreshTokenAsync(accountToken.RefreshToken, accountToken.Provider, cancellationToken);

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
            logger.LogInformation("刷新 Token 成功: {Name}({Provider}-{AuthMethod})", 
                accountToken.Name, accountToken.Provider, accountToken.AuthMethod);
        }
    }

    /// <summary>
    /// 判断账户是否支持指定模型（选号热路径）
    /// 有白名单则校验白名单；无白名单则用上游模型列表（带缓存）与 baseline 取交集后校验
    /// </summary>
    public async Task<bool> IsModelSupportedAsync(AccountToken account, string requestedModel, CancellationToken ct = default)
    {
        // 1. 检查映射列表 (Mapping)
        var mapping = account.ModelMapping;
        if (mapping != null && mapping.Count > 0)
        {
            if (ResolveMapping(requestedModel, mapping) != null) return true;
        }

        // 2. 检查白名单 (Whitelist)
        var whitelist = account.ModelWhites;
        if (whitelist != null && whitelist.Count > 0)
        {
            if (whitelist.Contains(requestedModel, StringComparer.OrdinalIgnoreCase)) return true;
            return whitelist.Where(k => k.EndsWith('*'))
                .Any(k => requestedModel.StartsWith(k[..^1], StringComparison.OrdinalIgnoreCase));
        }

        // 3. 获取上游模型列表 (Upstream)
        IReadOnlyList<string>? upstreamModelIds = null;
        try
        {
            upstreamModelIds = await FetchAndCacheUpstreamModelsAsync(account, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IsModelSupportedAsync 上游模型检测失败，将回退到基准配置：Name={Name}, Provider={Provider}, AuthMethod={AuthMethod}", 
                account.Name, account.Provider, account.AuthMethod);
        }

        // 4. 上游优先判断 (严格模式)
        // 如果上游列表获取成功且不为空，则基于上游列表进行判词（事实证明原则）
        if (upstreamModelIds != null && upstreamModelIds.Count > 0)
        {
            return upstreamModelIds.Contains(requestedModel, StringComparer.OrdinalIgnoreCase);
        }

        // 5. 基准模型兜底 (Baseline)
        // 仅在上游列表获取失败或结果为空时，使用系统内置基准检测
        var baselineModels = modelProvider.GetAvailableModels(account.Provider);
        if (baselineModels == null || baselineModels.Count == 0) return true;

        // 检查基准中的精确匹配
        if (baselineModels.Any(m => !m.Value.Contains('*') && m.Value.Equals(requestedModel, StringComparison.OrdinalIgnoreCase))) 
            return true;

        // 检查基准中的通配符匹配 (如 gpt-4-*)
        if (baselineModels.Where(m => m.Value.EndsWith('*'))
            .Any(m => requestedModel.StartsWith(m.Value[..^1], StringComparison.OrdinalIgnoreCase))) 
            return true;

        // 最后：如果系统定义了该 Provider 的基准但未命中，则返回 false；若基准根本没定义，则返回 true
        return false;
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
        // 1. 优先尝试从缓存获取（增加异常保护，确保缓存组件故障不影响业务）
        try
        {
            var cachedValue = await cache.GetStringAsync(CacheKey(account.Id), ct);
            if (!string.IsNullOrEmpty(cachedValue))
            {
                var cachedIds = JsonSerializer.Deserialize<List<string>>(cachedValue);
                if (cachedIds != null && cachedIds.Count > 0)
                {
                    return cachedIds;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "从缓存获取上游模型失败（降级请求上游）: Name={Name}, Provider={Provider}, AuthMethod={AuthMethod}",
                account.Name, account.Provider, account.AuthMethod);
        }

        // 2. 缓存不存在或探测失败，发起上游请求
        try
        {
            var handler = chatModelHandlerFactory.CreateHandler(
                account.Provider,
                account.AuthMethod,
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

            // 3. 异步写入缓存（增加异常保护，不因写缓存失败而导致请求报错）
            try
            {
                var ttl = MinTtl + TimeSpan.FromMinutes(Random.Shared.NextDouble() * (MaxTtl - MinTtl).TotalMinutes);
                await cache.SetStringAsync(
                    CacheKey(account.Id),
                    JsonSerializer.Serialize(ids),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                    ct);

                logger.LogInformation("同步并刷新上游模型缓存成功: Name={Name}, Provider={Provider}, Count={Count}, TTL={TTL}min",
                    account.Name, account.Provider, ids.Count, (int)ttl.TotalMinutes);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "更新上游模型缓存失败: Name={Name}, Provider={Provider}",
                    account.Name, account.Provider);
            }

            return ids;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "上游模型拉取失败: Name={Name}, Provider={Provider}, AuthMethod={AuthMethod}",
                account.Name, account.Provider, account.AuthMethod);
            return null;
        }
    }

    private static string CacheKey(Guid accountId) => $"account:upstream-models:{accountId}";
}

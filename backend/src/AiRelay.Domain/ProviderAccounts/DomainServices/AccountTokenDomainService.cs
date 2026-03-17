using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.Extensions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using AiRelay.Domain.Shared.OAuth.Authorize;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.Lock.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账户令牌领域服务
/// </summary>
public class AccountTokenDomainService(
    IServiceProvider serviceProvider,
    IChatModelHandlerFactory chatModelHandlerFactory,
    IRepository<AccountToken, Guid> accountTokenRepository,
    ILock distributedLock,
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
            extraProperties);

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
}

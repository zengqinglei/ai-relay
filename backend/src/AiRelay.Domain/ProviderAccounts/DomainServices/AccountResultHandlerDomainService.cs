using AiRelay.Domain.ProviderAccounts.Entities;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账号失败处理领域服务
/// </summary>
public class AccountResultHandlerDomainService(
    AccountRateLimitDomainService rateLimitDomainService,
    AccountUsageCacheDomainService usageCacheDomainService,
    ILogger<AccountResultHandlerDomainService> logger)
{
    /// <summary>
    /// 处理账号失败：可重试错误触发指数退避熔断，达到阈值后永久禁用；鉴权失败直接永久禁用
    /// </summary>
    public async Task HandleFailureAsync(
        AccountToken account,
        int statusCode,
        string? errorContent,
        bool isRetryable,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        var isOfficialAccount = string.IsNullOrEmpty(account.BaseUrl);

        // 1. 可重试错误（429/5xx）-> 指数退避熔断
        if (isRetryable)
        {
            // 幂等保护：高并发下若账号已被锁定，跳过重复熔断
            var alreadyLocked = await rateLimitDomainService.IsRateLimitedAsync(account.Id, cancellationToken);
            if (alreadyLocked)
            {
                logger.LogDebug("账号 {AccountName} 已在熔断期，跳过重复熔断处理", account.Name);
                return;
            }

            TimeSpan lockDuration;

            if (retryAfter.HasValue && retryAfter.Value.TotalSeconds > 0)
            {
                // 优先使用 API 返回的 retry-after
                lockDuration = retryAfter.Value;
            }
            else
            {
                var newCount = await usageCacheDomainService.IncrementBackoffCountAsync(account.Id, cancellationToken);

                if (isOfficialAccount)
                {
                    // 官方账号：5分钟~5小时，5小时连续3次永久禁用
                    if (newCount > 7)
                    {
                        var permanentMsg = $"账号 {account.Name} 连续失败次数过多，已永久禁用，状态码: {statusCode}";
                        account.MarkAsError(permanentMsg);
                        logger.LogError(permanentMsg);
                        return;
                    }
                    var backoffSeconds = newCount switch
                    {
                        1 => 300,
                        2 => 900,
                        3 => 1800,
                        4 => 3600,
                        _ => 18000  // 第5~7次：5小时
                    };
                    lockDuration = TimeSpan.FromSeconds(backoffSeconds);
                }
                else
                {
                    // 非官方账号：30s~1小时，超过7次永久禁用
                    if (newCount > 7)
                    {
                        var permanentMsg = $"账号 {account.Name} 连续失败次数过多，已永久禁用，状态码: {statusCode}";
                        account.MarkAsError(permanentMsg);
                        logger.LogError(permanentMsg);
                        return;
                    }
                    var backoffSeconds = newCount switch
                    {
                        1 => 30,
                        2 => 180,
                        3 => 600,
                        4 => 1800,
                        5 => 3600,
                        6 => 10800,
                        _ => 18000  // 第7次：5小时
                    };
                    lockDuration = TimeSpan.FromSeconds(backoffSeconds);
                }
            }

            await rateLimitDomainService.LockAsync(account.Id, lockDuration, cancellationToken);

            var errorMsg = $"账号 {account.Name} 触发熔断 {(int)lockDuration.TotalSeconds}秒，状态码: {statusCode}";
            account.MarkAsRateLimited(lockDuration, errorMsg);
            logger.LogWarning(errorMsg);
            return;
        }

        // 2. 致命鉴权错误 -> 永久禁用
        // 官方账号：401/403 直接永久禁用
        // 非官方账号：isRetryable=false 时 statusCode 通常为 400/其他，不影响账号状态
        if (isOfficialAccount && statusCode is 401 or 403)
        {
            account.MarkAsError($"鉴权失败: HTTP {statusCode} {errorContent}");
            logger.LogError("账号 {AccountName} 发生 {StatusCode} 鉴权失败", account.Name, statusCode);
        }
    }
}

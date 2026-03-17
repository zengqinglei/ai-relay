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
    /// 处理账号失败
    /// </summary>
    /// <param name="account">账号实体</param>
    /// <param name="statusCode">HTTP 状态码</param>
    /// <param name="errorContent">错误内容</param>
    /// <param name="isRetryable">是否为可重试错误</param>
    /// <param name="retryAfter">重试延迟</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task HandleFailureAsync(
        AccountToken account,
        int statusCode,
        string? errorContent,
        bool isRetryable,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        // 1. 可重试错误（429/5xx）-> 熔断
        if (isRetryable)
        {
            // 幂等保护：高并发下若账号已被锁定，跳过重复熔断
            // 避免多个并发失败请求叠加递增退避计数，导致瞬间跳到最大退避时间
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
                // 先递增计数拿到新值，再计算退避时长，最后用退避时长作为计数 TTL
                // TTL 与熔断时长绑定：熔断解除后计数自动过期，下次失败重新从 30s 开始
                var newCount = await usageCacheDomainService.IncrementBackoffCountAsync(account.Id, cancellationToken);
                var backoffSeconds = newCount switch
                {
                    1 => 30,
                    2 => 180,
                    3 => 600,
                    4 => 1800,
                    _ => 3600
                };
                lockDuration = TimeSpan.FromSeconds(backoffSeconds);

                // 累加退避计数
                await usageCacheDomainService.IncrementBackoffCountAsync(account.Id, cancellationToken);
            }

            await rateLimitDomainService.LockAsync(account.Id, lockDuration, cancellationToken);

            var errorMsg = $"账号 {account.Name} 触发熔断 {(int)lockDuration.TotalSeconds}秒，状态码: {statusCode}";
            account.MarkAsRateLimited(lockDuration, errorMsg);

            logger.LogWarning(errorMsg);

            return;
        }

        // 2. 致命鉴权错误（401/403）-> 永久禁用
        if (statusCode is 401 or 403)
        {
            account.MarkAsError($"鉴权失败: HTTP {statusCode} {errorContent}");

            logger.LogError(
                "账号 {AccountName} 发生 {StatusCode} 鉴权失败",
                account.Name,
                statusCode);
        }
    }
}

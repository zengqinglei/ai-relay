using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账号失败处理领域服务
/// </summary>
public class AccountResultHandlerDomainService(
    AccountRateLimitDomainService rateLimitDomainService,
    ILogger<AccountResultHandlerDomainService> logger)
{
    /// <summary>
    /// 处理账号失败：
    /// 1. 可重试错误触发熔断（账号级或模型级）
    /// 2. 达到阈值后官方账号可进入永久禁用
    /// 3. 官方账号鉴权失败直接永久禁用
    /// </summary>
    public async Task HandleFailureAsync(
        AccountToken account,
        int statusCode,
        string? errorContent,
        bool isRetryable,
        TimeSpan? retryAfter,
        string? downModelId,
        string? upModelId,
        CancellationToken cancellationToken)
    {
        var isOfficialAccount = string.IsNullOrEmpty(account.BaseUrl);
        var useModelScope = account.RateLimitScope == RateLimitScope.Model && !string.IsNullOrWhiteSpace(upModelId);

        // 1. 可重试错误（429/5xx/流崩溃等）-> 熔断
        if (isRetryable)
        {
            // 幂等保护：高并发下若账号已被锁定，跳过重复熔断
            var alreadyLocked = useModelScope
                ? await rateLimitDomainService.IsModelRateLimitedAsync(account.Id, upModelId!, cancellationToken)
                : await rateLimitDomainService.IsRateLimitedAsync(account.Id, cancellationToken);

            if (alreadyLocked)
            {
                logger.LogDebug("账号 {AccountName} 已在熔断期，跳过重复熔断处理", account.Name);
                return;
            }

            var lockDuration = await ResolveLockDurationAsync(
                account,
                isOfficialAccount,
                useModelScope,
                statusCode,
                retryAfter,
                cancellationToken,
                upModelId);

            // 账号级退避达到上限后，领域对象可能已被标记为 Error，此时不再继续加锁
            if (account.Status == AccountStatus.Error)
            {
                return;
            }

            if (useModelScope)
            {
                await rateLimitDomainService.LockModelAsync(account.Id, upModelId!, lockDuration, cancellationToken);
                var displayName = string.Equals(downModelId, upModelId, StringComparison.OrdinalIgnoreCase)
                    ? upModelId
                    : $"{downModelId} -> {upModelId}";
                var errorMsg = $"模型 {displayName} 触发熔断 {(int)lockDuration.TotalSeconds}秒，状态码: {statusCode}";
                account.MarkAsModelRateLimited(upModelId!, displayName, lockDuration, errorMsg);
                logger.LogWarning(errorMsg);
                return;
            }

            await rateLimitDomainService.LockAsync(account.Id, lockDuration, cancellationToken);
            var accountErrorMsg = $"账号 {account.Name} 触发熔断 {(int)lockDuration.TotalSeconds}秒，状态码: {statusCode}";
            account.MarkAsRateLimited(lockDuration, accountErrorMsg);
            logger.LogWarning(accountErrorMsg);
            return;
        }

        // 2. 致命鉴权错误 -> 永久禁用
        if (isOfficialAccount && statusCode is 401 or 403)
        {
            account.MarkAsError($"鉴权失败: HTTP {statusCode} {errorContent}");
            logger.LogError("账号 {AccountName} 发生 {StatusCode} 鉴权失败", account.Name, statusCode);
        }
    }

    /// <summary>
    /// 解析本次失败对应的熔断时长。
    /// </summary>
    private async Task<TimeSpan> ResolveLockDurationAsync(
        AccountToken account,
        bool isOfficialAccount,
        bool useModelScope,
        int statusCode,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken,
        string? upModelId)
    {
        if (retryAfter.HasValue)
        {
            return retryAfter.Value.TotalSeconds > 0
                ? retryAfter.Value
                : TimeSpan.FromSeconds(Random.Shared.Next(1, 4));
        }

        var newCount = useModelScope
            ? await rateLimitDomainService.IncrementModelBackoffCountAsync(account.Id, upModelId!, cancellationToken)
            : await rateLimitDomainService.IncrementBackoffCountAsync(account.Id, cancellationToken);

        // 仅账号级模式支持把整个账号永久打入 Error；模型级模式最多长期锁模型，不封整个账号
        if (!useModelScope)
        {
            var permanentThreshold = isOfficialAccount ? 8 : 11;
            if (newCount > permanentThreshold)
            {
                var permanentMsg = $"账号 {account.Name} 连续失败次数过多，已永久禁用，状态码: {statusCode}";
                account.MarkAsError(permanentMsg);
                logger.LogError(permanentMsg);
                return TimeSpan.Zero;
            }
        }

        var boundedCount = useModelScope
            ? Math.Min(newCount, isOfficialAccount ? 8 : 11)
            : newCount;

        // 官方账号退避策略（对齐三级恢复周期）：
        // count 1~2：短暂等待（5min/30min），容忍瞬时过载
        // count 3~5：以2小时为间隔探测5小时窗口是否恢复（共3次，累计覆盖完整5h窗口）
        // count 6~7：以12小时为间隔探测1天限额是否恢复（共2次，覆盖完整24h周期）
        // count 8：长期等待（2.5天，对齐7天领取周期的1/3）
        // count ≥9：永久禁用
        var backoffSeconds = isOfficialAccount
            ? boundedCount switch
            {
                1 => 300,       // 5分钟
                2 => 1800,      // 30分钟
                3 => 7200,      // 2小时（探测5小时窗口①）
                4 => 7200,      // 2小时（探测5小时窗口②）
                5 => 7200,      // 2小时（探测5小时窗口③）
                6 => 43200,     // 12小时（探测1天限额①）
                7 => 43200,     // 12小时（探测1天限额②）
                _ => 216000     // 第8次：2.5天
            }
            // 非官方账号退避策略（底层同为 Claude 账号，三级恢复周期一致）：
            // count 1~5：30分钟内密集短间隔重试，容忍非官方中转商的瞬时抖动
            // count 6~8：以2小时为间隔探测5小时窗口是否恢复（共3次）
            // count 9~10：以12小时为间隔探测1天限额是否恢复（共2次）
            // count 11：长期等待（2.5天，对齐7天领取周期的1/3）
            // count ≥12：永久禁用
            : boundedCount switch
            {
                1 => 30,        // 30秒
                2 => 120,       // 2分钟
                3 => 300,       // 5分钟
                4 => 900,       // 15分钟
                5 => 1800,      // 30分钟
                6 => 7200,      // 2小时（探测5小时窗口①）
                7 => 7200,      // 2小时（探测5小时窗口②）
                8 => 7200,      // 2小时（探测5小时窗口③）
                9 => 43200,     // 12小时（探测1天限额①）
                10 => 43200,    // 12小时（探测1天限额②）
                _ => 216000     // 第11次：2.5天
            };

        return TimeSpan.FromSeconds(backoffSeconds);
    }
}



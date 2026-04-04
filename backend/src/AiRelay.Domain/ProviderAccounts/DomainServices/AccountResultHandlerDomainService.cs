using AiRelay.Domain.ProviderAccounts.Entities;
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
                var newCount = await rateLimitDomainService.IncrementBackoffCountAsync(account.Id, cancellationToken);

                if (isOfficialAccount)
                {
                    // 官方账号退避策略（对齐三级恢复周期）：
                    // count 1~2：短暂等待（5min/30min），容忍瞬时过载
                    // count 3~5：以2小时为间隔探测5小时窗口是否恢复（共3次，累计覆盖完整5h窗口）
                    // count 6~7：以12小时为间隔探测1天限额是否恢复（共2次，覆盖完整24h周期）
                    // count 8：长期等待（2.5天，对齐7天领取周期的1/3）
                    // count ≥9：永久禁用
                    if (newCount > 8)
                    {
                        var permanentMsg = $"账号 {account.Name} 连续失败次数过多，已永久禁用，状态码: {statusCode}";
                        account.MarkAsError(permanentMsg);
                        logger.LogError(permanentMsg);
                        return;
                    }
                    var backoffSeconds = newCount switch
                    {
                        1 => 300,       // 5分钟
                        2 => 1800,      // 30分钟
                        3 => 7200,      // 2小时（探测5小时窗口①）
                        4 => 7200,      // 2小时（探测5小时窗口②）
                        5 => 7200,      // 2小时（探测5小时窗口③）
                        6 => 43200,     // 12小时（探测1天限额①）
                        7 => 43200,     // 12小时（探测1天限额②）
                        _ => 216000     // 第8次：2.5天
                    };
                    lockDuration = TimeSpan.FromSeconds(backoffSeconds);
                }
                else
                {
                    // 非官方账号退避策略（底层同为 Claude 账号，三级恢复周期一致）：
                    // count 1~5：30分钟内密集短间隔重试，容忍非官方中转商的瞬时抖动
                    // count 6~8：以2小时为间隔探测5小时窗口是否恢复（共3次）
                    // count 9~10：以12小时为间隔探测1天限额是否恢复（共2次）
                    // count 11：长期等待（2.5天，对齐7天领取周期的1/3）
                    // count ≥12：永久禁用
                    if (newCount > 11)
                    {
                        var permanentMsg = $"账号 {account.Name} 连续失败次数过多，已永久禁用，状态码: {statusCode}";
                        account.MarkAsError(permanentMsg);
                        logger.LogError(permanentMsg);
                        return;
                    }
                    var backoffSeconds = newCount switch
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

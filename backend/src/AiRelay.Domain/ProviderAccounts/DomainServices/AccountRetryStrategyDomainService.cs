using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Microsoft.Extensions.Options;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账号重试策略领域服务，负责纯粹的业务规则计算：决定下一步指令及退避延迟
/// </summary>
public class AccountRetryStrategyDomainService(IOptions<Options.ModelSchedulingOptions> schedulingOptions)
{
    private readonly Options.ModelSchedulingOptions _schedulingOptions = schedulingOptions.Value;

    /// <summary>
    /// 当并发槽位不可用时，决定是否切号或直接失败。
    /// </summary>
    public FailureInstruction DetermineConcurrencyFailureInstruction(
        bool shouldWait,
        bool waitQueueFull,
        bool waitTimedOut)
    {
        if (!shouldWait || waitQueueFull || waitTimedOut)
            return FailureInstruction.SwitchAccount;

        return FailureInstruction.Fail;
    }

    /// <summary>
    /// 分析首包前流失败，统一生成可用于后续重试决策的错误分析结果。
    /// </summary>
    public ModelErrorAnalysisResult AnalyzePreCommitStreamFailure(
        bool isEmptyStream,
        bool isHealthCheckError,
        bool isIoException,
        string? detail = null)
    {
        var description = isEmptyStream
            ? "流健康检查未读取到包含有效文本，判定为空流或无响应"
            : isHealthCheckError
                ? detail ?? "流健康检查到内部错误事件节点 'unknown'"
                : isIoException
                    ? $"上游流在首包前断开，触发同号重试或切号重试机制: {detail}"
                    : $"流尚未开始下发便中断异常，触发同号重试或切号重试机制: {detail}";

        return new ModelErrorAnalysisResult
        {
            RetryType = RetryType.RetrySameAccount,
            Description = description
        };
    }

    public (FailureInstruction Instruction, TimeSpan RetryDelay) DetermineFailureInstruction(
        ModelErrorAnalysisResult retryPolicy,
        int currentRetryCount,
        int maxRetries,
        int accountSwitchCount)
    {
        // 0. 端点不支持：直接透传，不重试不切号不计熔断
        if (retryPolicy.RetryType == RetryType.UnsupportedEndpoint)
            return (FailureInstruction.Fail, TimeSpan.Zero);

        var canRetry = retryPolicy.RetryType is RetryType.RetrySameAccount
                                              or RetryType.RetrySameAccountWithDowngrade;

        // 1. 同账号重试判定
        if (canRetry && currentRetryCount < maxRetries)
        {
            // 如果上游明确要求等待过久，通常意味着该账号被严重限流，优先切号
            if (retryPolicy.RetryAfter.HasValue &&
                retryPolicy.RetryAfter.Value.TotalSeconds >= _schedulingOptions.LongRetryAfterSwitchThresholdSeconds)
            {
                return (FailureInstruction.SwitchAccount, TimeSpan.Zero);
            }

            var delay = retryPolicy.RetryAfter ?? CalculateRetryDelay(currentRetryCount);
            return (FailureInstruction.RetrySameAccount, delay);
        }

        // 2. 切换账号判定
        // 情况 A: Handler 判定可重试，但同账号次数已满，则切换到下一个号继续试
        if (canRetry)
            return (FailureInstruction.SwitchAccount, TimeSpan.Zero);

        // 情况 B: Handler 判定不可同号重试（如官方 5xx 或 401/403 等）
        // 第一个号报错时额外给一次切号机会（盲切补偿）
        if (retryPolicy.RetryType == RetryType.NoRetry && accountSwitchCount == 0)
            return (FailureInstruction.SwitchAccount, TimeSpan.Zero);

        // 其他情况（已切过号但还是报错）则直接失败
        return (FailureInstruction.Fail, TimeSpan.Zero);
    }

    /// <summary>
    /// 计算指数退避重试延迟（含随机抖动）。
    /// </summary>
    protected virtual TimeSpan CalculateRetryDelay(int currentRetryCount)
    {
        var jitter = Random.Shared.NextDouble() * 0.4 + 0.8; // [0.8, 1.2)
        return TimeSpan.FromMilliseconds(1000 * Math.Pow(2, currentRetryCount) * jitter);
    }
}


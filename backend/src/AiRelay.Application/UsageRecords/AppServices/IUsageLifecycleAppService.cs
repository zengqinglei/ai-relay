using AiRelay.Application.UsageRecords.Dtos.Lifecycle;

namespace AiRelay.Application.UsageRecords.AppServices;

/// <summary>
/// 使用记录生命周期应用服务接口
/// </summary>
public interface IUsageLifecycleAppService
{
    /// <summary>开始记录使用（INSERT UsageRecord，Status=InProgress）</summary>
    Task<StartUsageOutputDto> StartUsageAsync(StartUsageInputDto input, CancellationToken cancellationToken = default);

    /// <summary>开始单次上游尝试记录（INSERT UsageRecordAttempt，Status=InProgress）</summary>
    Task StartAttemptAsync(StartAttemptInputDto input, CancellationToken cancellationToken = default);

    /// <summary>完成单次上游尝试记录（UPDATE UsageRecordAttempt 为最终状态）</summary>
    Task CompleteAttemptAsync(CompleteAttemptInputDto input, CancellationToken cancellationToken = default);

    /// <summary>完成记录使用（UPDATE UsageRecord 为最终状态）</summary>
    Task FinishUsageAsync(FinishUsageInputDto input, CancellationToken cancellationToken = default);
}

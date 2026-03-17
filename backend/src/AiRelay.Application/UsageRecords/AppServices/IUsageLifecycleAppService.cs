using AiRelay.Application.UsageRecords.Dtos.Lifecycle;

namespace AiRelay.Application.UsageRecords.AppServices;

/// <summary>
/// 使用记录生命周期应用服务接口
/// </summary>
public interface IUsageLifecycleAppService
{
    /// <summary>
    /// 开始记录使用
    /// </summary>
    Task<StartUsageOutputDto> StartUsageAsync(StartUsageInputDto input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成记录使用
    /// </summary>
    Task FinishUsageAsync(FinishUsageInputDto input, CancellationToken cancellationToken = default);
}

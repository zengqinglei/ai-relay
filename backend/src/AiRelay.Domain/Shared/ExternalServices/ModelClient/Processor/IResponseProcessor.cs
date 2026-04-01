using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

/// <summary>
/// 响应处理器接口
/// 每个 Processor 处理同一个 StreamEvent 实例，按链式顺序执行
/// 若一个 Processor (如 HandleError) 修改了内容并标为 IsComplete，后续 Processor 可根据需要停止处理
/// </summary>
public interface IResponseProcessor
{
    /// <summary>
    /// 是否需要阻断原始流并篡改数据（如 ToCompletion 需要将流重新打包）
    /// 如果全部为 false，网关将开启 0损耗 的 Fast-Pass 模式，直接透传网络 Chunk。
    /// </summary>
    bool RequiresMutation { get; }

    /// <summary>
    /// 处理响应事件（就地修改 evt 中的字段）
    /// </summary>
    Task ProcessAsync(StreamEvent evt, CancellationToken ct);
}

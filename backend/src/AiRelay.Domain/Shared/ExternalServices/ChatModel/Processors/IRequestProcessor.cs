using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;

/// <summary>
/// 请求处理器：读取 down（只读），写入/修改 up（可变）
/// Processor 自身不声明 Supports 条件，由 Handler.GetProcessors(down, degradationLevel) 组合决策
/// 执行顺序由 Handler.GetProcessors() 返回的 List 顺序决定
/// </summary>
public interface IRequestProcessor
{
    Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct);
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

namespace AiRelay.Application.ModelRoutes.Handlers;

/// <summary>
/// 路由数据下发响应处理器接口，用于隔离代理端(Http Body写出)与聊天端(Channel/StreamEvent 写出)的差异。
/// </summary>
public interface IRouteResponseHandler
{
    /// <summary>
    /// 是否已经开始下发响应
    /// </summary>
    bool HasResponseStarted { get; }

    /// <summary>
    /// 当前事件是否应该由该处理器接收并下发
    /// </summary>
    bool ShouldHandle(StreamEvent streamEvent, byte[]? bytesToForward);

    /// <summary>
    /// 首包成功时回调（代理需要写 Http Header，Chat 可以忽略）
    /// </summary>
    Task OnHeadersReadyAsync(int statusCode, Dictionary<string, IEnumerable<string>> headers, CancellationToken ct);

    /// <summary>
    /// 数据流转时回调
    /// </summary>
    Task OnDataAsync(StreamEvent streamEvent, byte[]? originalBytes, CancellationToken ct);

    /// <summary>
    /// 发生无法挽回的终端错误时回调
    /// </summary>
    Task<string?> OnTerminalErrorAsync(RouteTerminalError error, CancellationToken ct);

    /// <summary>
    /// 强制中断连接
    /// </summary>
    void AbortConnection();
}

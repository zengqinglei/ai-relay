using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AiRelay.Application.ModelRoutes.Handlers;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.Utilities;

namespace AiRelay.Application.ChatSessions.Handlers;

public class ChatRouteResponseHandler(Channel<StreamEvent> channel) : IRouteResponseHandler
{
    private bool _hasStarted = false;

    public bool HasResponseStarted => _hasStarted;

    public bool ShouldHandle(StreamEvent streamEvent, byte[]? bytesToForward)
    {
        return !string.IsNullOrEmpty(streamEvent.Content)
               || !string.IsNullOrEmpty(streamEvent.ReasoningContent)
               || streamEvent.InlineData is { Count: > 0 }
               || streamEvent.IsComplete
               || streamEvent.Type is StreamEventType.Error or StreamEventType.System;
    }

    public Task OnHeadersReadyAsync(int statusCode, Dictionary<string, IEnumerable<string>> headers, CancellationToken ct)
    {
        // 对于工作区聊天，只有真正写出首个事件时才算开始响应。
        // 这样才能与代理入口的健康流检查语义保持一致：首包前异常仍可输出终端错误事件。
        return Task.CompletedTask;
    }

    public async Task OnDataAsync(StreamEvent streamEvent, byte[]? originalBytes, CancellationToken ct)
    {
        _hasStarted = true;
        await channel.Writer.WriteAsync(streamEvent, ct);
    }

    public async Task<string?> OnTerminalErrorAsync(RouteTerminalError error, CancellationToken ct)
    {
        var message = BuildMessage(error);

        await channel.Writer.WriteAsync(new StreamEvent
        {
            Type = StreamEventType.Error,
            Content = message,
            IsComplete = true
        }, ct);

        channel.Writer.Complete();
        return message;
    }

    private static string BuildMessage(RouteTerminalError error)
    {
        var message = error.Kind == RouteTerminalErrorKind.UpstreamNormalized
            ? ErrorMessageExtractor.TryExtractMessage(error.ErrorBody) ?? "Service Temporarily Unavailable"
            : error.Exception?.Message ?? error.ErrorBody ?? "未知错误";

        return error.Kind == RouteTerminalErrorKind.UpstreamNormalized
            ? message
            : $"代理网关异常被拦截: {message}";
    }



    public void AbortConnection()
    {
        channel.Writer.Complete(new OperationCanceledException("连接被主动终止"));
    }
}

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

/// <summary>
/// 两阶段发送模式的响应结果
/// Phase 1（SendAsync）完成时即可得到：IsSuccess / StatusCode / Headers / ErrorBody
/// Phase 2（Events 枚举）：按需消费响应体，状态机闭包负责释放 HttpResponseMessage
/// </summary>
public sealed record ProxyResponse(
    /// <summary>是否成功（由上游 HttpResponseMessage.IsSuccessStatusCode 决定）</summary>
    bool IsSuccess,

    /// <summary>上游 HTTP 状态码</summary>
    int StatusCode,

    /// <summary>上游响应头（含 Content-Type、Content-Length 等）</summary>
    Dictionary<string, IEnumerable<string>> Headers,

    /// <summary>
    /// 请求失败时的响应体（非流式读取），成功时为 null。
    /// 调用方可直接用于重试判断，无需消费 Events。
    /// </summary>
    string? ErrorBody,

    /// <summary>
    /// 成功时的响应事件流（Phase 2）；失败时为 null。
    /// 枚举完成或取消时，HttpResponseMessage 由迭代器状态机自动 Dispose。
    /// </summary>
    IAsyncEnumerable<StreamEvent>? Events
);

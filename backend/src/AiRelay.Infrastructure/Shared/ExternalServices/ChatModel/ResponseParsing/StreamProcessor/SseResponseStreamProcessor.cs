using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.TokenCalculate;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;

/// <summary>
/// SSE 响应流处理器
/// </summary>
public class SseResponseStreamProcessor(
    ILogger<SseResponseStreamProcessor> logger)
{
    /// <summary>
    /// 解析 SSE 响应流并返回事件流（假设调用方已判断 IsSuccessStatusCode）
    /// </summary>
    public async IAsyncEnumerable<ChatStreamEvent> ParseSseStreamAsync(
        HttpResponseMessage response,
        IResponseParser responseParser,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType != null && !contentType.Contains("text/event-stream") && !contentType.Contains("application/json"))
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("上游返回了非预期的 Content-Type: {ContentType}, StatusCode: {response.StatusCode}, Content: {Content}",
                contentType,
                response.StatusCode,
                errorContent.Length > 500 ? errorContent[..500] + "..." : errorContent);
            var showErrorContent = errorContent.Length > 100 ? errorContent[..100] + "..." : errorContent;

            yield return new ChatStreamEvent(Error: $"上游返回了非预期的响应：Content-Type: {contentType}, StatusCode: {response.StatusCode}, Content: {showErrorContent}");
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            var part = responseParser.ParseChunk(line);
            if (part == null) continue;

            if (part.IsComplete) break;

            if (!string.IsNullOrEmpty(part.Error))
            {
                logger.LogWarning("上游返回错误: {Error}", part.Error);
                yield return new ChatStreamEvent(Error: part.Error);
                yield break;
            }

            if (!string.IsNullOrEmpty(part.Content))
            {
                yield return new ChatStreamEvent(Content: part.Content);
            }

            if (part.InlineData != null)
            {
                yield return new ChatStreamEvent(InlineData: part.InlineData);
            }
        }

        yield return new ChatStreamEvent(IsComplete: true);
    }

    /// <summary>
    /// 转发响应流到目标流（代理场景）
    /// </summary>
    public async Task<StreamForwardResult> ForwardResponseAsync(
        HttpResponseMessage response,
        Stream targetStream,
        IResponseParser parser,
        bool isStreaming,
        ForwardResponseOptions options,
        CancellationToken cancellationToken = default)
    {
        var accumulator = new TokenUsageAccumulator();
        var sseBuffer = isStreaming ? new SseStreamBuffer() : null;
        var fullBodyBuffer = !isStreaming ? new MemoryStream() : null;
        var capturedStream = options.CaptureBody ? new MemoryStream() : null;
        var truncated = false;

        try
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = ArrayPool<byte>.Shared.Rent(8192);

            try
            {
                int bytesRead;
                while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    var span = buffer.AsSpan(0, bytesRead);

                    // 响应体捕获（需要在 await 前完成，避免 span 跨边界）
                    if (capturedStream != null && !truncated)
                    {
                        if (capturedStream.Length >= options.MaxCaptureLength)
                        {
                            truncated = true;
                        }
                        else
                        {
                            var remaining = options.MaxCaptureLength - capturedStream.Length;
                            if (span.Length <= remaining)
                                capturedStream.Write(span);
                            else
                            {
                                capturedStream.Write(span.Slice(0, (int)remaining));
                                truncated = true;
                            }
                        }
                    }

                    // Token 统计 + 响应转换
                    if (isStreaming)
                    {
                        foreach (var line in sseBuffer!.ProcessChunk(span))
                        {
                            options.OnSseLine?.Invoke(line);
                            var part = parser.ParseChunk(line);
                            if (part != null)
                            {
                                accumulator.Add(part.Usage);
                                accumulator.SetModelId(part.ModelId);
                            }

                            // 响应格式转换
                            if (options.NeedsResponseConversion && options.ResponseConverter != null)
                            {
                                foreach (var convertedLine in options.ResponseConverter(line))
                                {
                                    var bytes = Encoding.UTF8.GetBytes(convertedLine + "\n\n");
                                    await targetStream.WriteAsync(bytes, cancellationToken);
                                }
                                await targetStream.FlushAsync(cancellationToken);
                            }
                        }
                    }
                    else
                    {
                        fullBodyBuffer!.Write(span);
                    }

                    // 转发字节流（仅在非转换模式下）
                    if (!options.NeedsResponseConversion)
                    {
                        await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        await targetStream.FlushAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            // 流式：刷新 SSE 缓冲区残留数据
            if (isStreaming && sseBuffer != null)
            {
                foreach (var line in sseBuffer.Flush())
                {
                    options.OnSseLine?.Invoke(line);
                    var part = parser.ParseChunk(line);
                    if (part != null)
                    {
                        accumulator.Add(part.Usage);
                        accumulator.SetModelId(part.ModelId);
                    }

                    // 响应格式转换（残留数据）
                    if (options.NeedsResponseConversion && options.ResponseConverter != null)
                    {
                        foreach (var convertedLine in options.ResponseConverter(line))
                        {
                            var bytes = Encoding.UTF8.GetBytes(convertedLine + "\n\n");
                            await targetStream.WriteAsync(bytes, cancellationToken);
                        }
                    }
                }

                // 转换模式下最后 flush
                if (options.NeedsResponseConversion)
                {
                    await targetStream.FlushAsync(cancellationToken);
                }
            }

            // 非流式响应解析
            if (!isStreaming && fullBodyBuffer != null)
            {
                fullBodyBuffer.Position = 0;
                using var reader = new StreamReader(fullBodyBuffer, Encoding.UTF8);
                var json = await reader.ReadToEndAsync(cancellationToken);
                var result = parser.ParseCompleteResponse(json);
                accumulator.Add(result.Usage);
                accumulator.SetModelId(result.ModelId);
            }

            var capturedBody = capturedStream != null ? GetCapturedBody(capturedStream, truncated) : null;

            if (isStreaming && accumulator.InputTokens == 0 && accumulator.OutputTokens == 0)
            {
                logger.LogWarning("流式响应 Token 统计为 0，可能存在解析问题。ModelId: {ModelId}", accumulator.ModelId);
            }

            return new StreamForwardResult(
                new ResponseUsage(
                    accumulator.InputTokens,
                    accumulator.OutputTokens,
                    accumulator.CacheReadTokens,
                    accumulator.CacheCreationTokens),
                accumulator.ModelId,
                capturedBody);
        }
        finally
        {
            fullBodyBuffer?.Dispose();
            capturedStream?.Dispose();
        }
    }

    private static string? GetCapturedBody(MemoryStream stream, bool truncated)
    {
        if (stream.Length == 0) return null;
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var content = reader.ReadToEnd();
        return truncated ? content + "...[Truncated]" : content;
    }
}

public record ForwardResponseOptions(
    bool CaptureBody,
    int MaxCaptureLength,
    Action<string>? OnSseLine,
    bool NeedsResponseConversion = false,
    Func<string, IEnumerable<string>>? ResponseConverter = null);

public record StreamForwardResult(
    ResponseUsage Usage,
    string? ModelId,
    string? CapturedBody);

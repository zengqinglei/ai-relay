namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;

/// <summary>
/// 包装流以阻止 Dispose 传播到底层流。
/// 用于 StreamContent 在 Fallback 重试场景中防止底层 RawStream 被提前关闭。
/// StreamContent.Dispose() → LeaveOpenStream.Dispose()（空操作） → 底层流保持可用
/// </summary>
internal sealed class LeaveOpenStream(Stream inner) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }

    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => inner.ReadAsync(buffer, offset, count, ct);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        => inner.ReadAsync(buffer, ct);

    // 核心：阻止 Dispose 传播，保持底层流可用
    protected override void Dispose(bool disposing) { /* intentionally empty */ }
    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

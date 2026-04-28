namespace AiRelay.Application.ModelRoutes.Dtos;

/// <summary>
/// 并发槽位资源，利用 IAsyncDisposable 确保生命周期结束时自动释放
/// </summary>
public sealed class ConcurrencySlot : IAsyncDisposable
{
    private readonly Func<Task>? _releaseAction;

    /// <summary>
    /// 是否成功获取到了并发槽位
    /// </summary>
    public bool Acquired { get; }

    public ConcurrencySlot(bool acquired, Func<Task>? releaseAction = null)
    {
        Acquired = acquired;
        _releaseAction = releaseAction;
    }

    public async ValueTask DisposeAsync()
    {
        if (Acquired && _releaseAction != null)
        {
            try
            {
                await _releaseAction();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Failed to release concurrency slot: {0}", ex);
            }
        }
    }
}

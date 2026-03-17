namespace Leistd.Tracing.Core.Services;

public class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly AsyncLocal<string?> _currentCorrelationId = new();

    public string? Get()
    {
        return _currentCorrelationId.Value;
    }

    public string Create()
    {
        return Guid.NewGuid().ToString("N");
    }

    public IDisposable Change(string correlationId)
    {
        var parent = _currentCorrelationId.Value;
        _currentCorrelationId.Value = correlationId;

        return new DisposableAction(() =>
        {
            _currentCorrelationId.Value = parent;
        });
    }

    private class DisposableAction(Action action) : IDisposable
    {
        public void Dispose()
        {
            action();
        }
    }
}

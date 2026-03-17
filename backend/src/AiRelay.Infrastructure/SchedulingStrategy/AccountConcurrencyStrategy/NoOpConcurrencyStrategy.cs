using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;

namespace AiRelay.Infrastructure.SchedulingStrategy.AccountConcurrencyStrategy;

public class NoOpConcurrencyStrategy : IConcurrencyStrategy
{
    public Task<bool> AcquireSlotAsync(Guid accountTokenId, Guid requestId, int maxConcurrency, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task ReleaseSlotAsync(Guid accountTokenId, Guid requestId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<int> GetConcurrencyCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public Task<IReadOnlyDictionary<Guid, int>> GetConcurrencyCountsAsync(IEnumerable<Guid> accountTokenIds, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IReadOnlyDictionary<Guid, int>)new Dictionary<Guid, int>());
    }

    public Task<bool> IncrementWaitCountAsync(Guid accountTokenId, int maxWait, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task DecrementWaitCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<int> GetWaitingCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public Task<bool> WaitForSlotAsync(
        Guid accountTokenId,
        Guid requestId,
        int maxConcurrency,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

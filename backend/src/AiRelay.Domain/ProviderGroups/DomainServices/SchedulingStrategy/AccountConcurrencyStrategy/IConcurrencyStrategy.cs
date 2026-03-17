namespace AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;

public interface IConcurrencyStrategy
{
    /// <summary>
    /// 尝试获取账户并发槽位
    /// </summary>
    /// <param name="accountTokenId">账户ID</param>
    /// <param name="requestId">请求ID（用于唯一标识）</param>
    /// <param name="maxConcurrency">最大并发数（小于等于0表示不限制）</param>
    /// <param name="cancellationToken"></param>
    /// <returns>是否获取成功</returns>
    Task<bool> AcquireSlotAsync(Guid accountTokenId, Guid requestId, int maxConcurrency, CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放账户并发槽位
    /// </summary>
    /// <param name="accountTokenId">账户ID</param>
    /// <param name="requestId">请求ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ReleaseSlotAsync(Guid accountTokenId, Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户当前并发数
    /// </summary>
    /// <param name="accountTokenId">账户ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns>当前并发数</returns>
    Task<int> GetConcurrencyCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取账户当前并发数
    /// </summary>
    /// <param name="accountTokenIds">账户ID列表</param>
    /// <param name="cancellationToken"></param>
    /// <returns>账户ID -> 当前并发数</returns>
    Task<IReadOnlyDictionary<Guid, int>> GetConcurrencyCountsAsync(IEnumerable<Guid> accountTokenIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 增加账户等待队列计数
    /// </summary>
    /// <param name="accountTokenId">账户ID</param>
    /// <param name="maxWait">最大等待数</param>
    /// <param name="cancellationToken"></param>
    /// <returns>是否成功（false 表示队列已满）</returns>
    Task<bool> IncrementWaitCountAsync(Guid accountTokenId, int maxWait, CancellationToken cancellationToken = default);

    /// <summary>
    /// 减少账户等待队列计数
    /// </summary>
    /// <param name="accountTokenId">账户ID</param>
    /// <param name="cancellationToken"></param>
    Task DecrementWaitCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户等待队列计数
    /// </summary>
    /// <param name="accountTokenId">账户ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns>等待队列计数</returns>
    Task<int> GetWaitingCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 等待获取并发槽位（指数退避策略）
    /// </summary>
    /// <param name="accountTokenId">账户ID</param>
    /// <param name="requestId">请求ID</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken"></param>
    /// <returns>是否成功获取槽位</returns>
    Task<bool> WaitForSlotAsync(
        Guid accountTokenId,
        Guid requestId,
        int maxConcurrency,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

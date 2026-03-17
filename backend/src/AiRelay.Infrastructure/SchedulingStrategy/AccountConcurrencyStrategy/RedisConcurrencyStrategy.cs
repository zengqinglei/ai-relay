using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using StackExchange.Redis;

namespace AiRelay.Infrastructure.SchedulingStrategy.AccountConcurrencyStrategy;

public class RedisConcurrencyStrategy(IConnectionMultiplexer connectionMultiplexer) : IConcurrencyStrategy
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private const string AccountSlotKeyPrefix = "concurrency:account:";
    private const string AccountWaitKeyPrefix = "wait:account:";
    private const int SlotTtlSeconds = 1800; // 30 minutes
    private const int WaitQueueTtlSeconds = 1800; // 30 minutes

    // Acquire Script - 使用 Redis TIME 命令获取服务器时间
    // @key: concurrency:account:{id}
    // @maxConcurrency: 最大并发数
    // @requestId: 请求唯一标识
    // @ttl: 过期时间（秒）
    private static readonly LuaScript AcquireScript = LuaScript.Prepare(@"
        local key = @key
        local maxConcurrency = tonumber(@maxConcurrency)
        local requestId = @requestId
        local ttl = tonumber(@ttl)

        -- 使用 Redis 服务器时间
        local timeResult = redis.call('TIME')
        local now = tonumber(timeResult[1])
        local cutoffTime = now - ttl

        -- 1. Cleanup expired slots
        redis.call('ZREMRANGEBYSCORE', key, '-inf', cutoffTime)

        -- 2. Check if already exists (re-entrant)
        if redis.call('ZSCORE', key, requestId) then
            redis.call('ZADD', key, now, requestId)
            redis.call('EXPIRE', key, ttl)
            return 1
        end

        -- 3. Check capacity
        local count = redis.call('ZCARD', key)
        if count < maxConcurrency then
            redis.call('ZADD', key, now, requestId)
            redis.call('EXPIRE', key, ttl)
            return 1
        end

        return 0
    ");

    // Release Script
    // @key: concurrency:account:{id}
    // @requestId: 请求唯一标识
    private static readonly LuaScript ReleaseScript = LuaScript.Prepare(@"
        local key = @key
        local requestId = @requestId
        return redis.call('ZREM', key, requestId)
    ");

    // GetCount Script - 使用 Redis TIME 命令
    // @key: concurrency:account:{id}
    // @ttl: 过期时间（秒）
    private static readonly LuaScript GetCountScript = LuaScript.Prepare(@"
        local key = @key
        local ttl = tonumber(@ttl)

        -- 使用 Redis 服务器时间
        local timeResult = redis.call('TIME')
        local now = tonumber(timeResult[1])
        local cutoffTime = now - ttl

        redis.call('ZREMRANGEBYSCORE', key, '-inf', cutoffTime)
        return redis.call('ZCARD', key)
    ");

    // IncrementWaitCount Script
    // @key: wait:account:{id}
    // @maxWait: 最大等待数
    // @ttl: 过期时间（秒）
    private static readonly LuaScript IncrementWaitScript = LuaScript.Prepare(@"
        local current = redis.call('GET', @key)
        if current == false then
            current = 0
        else
            current = tonumber(current)
        end

        if current >= tonumber(@maxWait) then
            return 0
        end

        redis.call('INCR', @key)
        redis.call('EXPIRE', @key, @ttl)
        return 1
    ");

    // DecrementWaitCount Script
    // @key: wait:account:{id}
    private static readonly LuaScript DecrementWaitScript = LuaScript.Prepare(@"
        local current = redis.call('GET', @key)
        if current ~= false and tonumber(current) > 0 then
            redis.call('DECR', @key)
        end
        return 1
    ");

    public async Task<bool> AcquireSlotAsync(Guid accountTokenId, Guid requestId, int maxConcurrency, CancellationToken cancellationToken = default)
    {
        if (maxConcurrency <= 0) return true;

        var key = GetAccountKey(accountTokenId);

        var result = await _database.ScriptEvaluateAsync(AcquireScript, new
        {
            key = (RedisKey)key,
            maxConcurrency,
            requestId = requestId.ToString(),
            ttl = SlotTtlSeconds
        });

        return (int)result == 1;
    }

    public async Task ReleaseSlotAsync(Guid accountTokenId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var key = GetAccountKey(accountTokenId);

        await _database.ScriptEvaluateAsync(ReleaseScript, new
        {
            key = (RedisKey)key,
            requestId = requestId.ToString()
        });
    }

    public async Task<int> GetConcurrencyCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default)
    {
        var key = GetAccountKey(accountTokenId);

        var result = await _database.ScriptEvaluateAsync(GetCountScript, new
        {
            key = (RedisKey)key,
            ttl = SlotTtlSeconds
        });

        return (int)result;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetConcurrencyCountsAsync(IEnumerable<Guid> accountTokenIds, CancellationToken cancellationToken = default)
    {
        var ids = accountTokenIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var batch = _database.CreateBatch();
        var tasks = new Dictionary<Guid, Task<RedisResult>>();

        foreach (var id in ids)
        {
            var key = GetAccountKey(id);
            tasks[id] = batch.ScriptEvaluateAsync(GetCountScript, new
            {
                key = (RedisKey)key,
                ttl = SlotTtlSeconds
            });
        }

        batch.Execute();
        await Task.WhenAll(tasks.Values);

        // 所有 Task 已完成，安全地提取结果
        return tasks.ToDictionary(
            kvp => kvp.Key,
            kvp => (int)kvp.Value.Result
        );
    }

    public async Task<bool> IncrementWaitCountAsync(Guid accountTokenId, int maxWait, CancellationToken cancellationToken = default)
    {
        var key = GetWaitKey(accountTokenId);

        var result = await _database.ScriptEvaluateAsync(IncrementWaitScript, new
        {
            key = (RedisKey)key,
            maxWait,
            ttl = WaitQueueTtlSeconds
        });

        return (int)result == 1;
    }

    public async Task DecrementWaitCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default)
    {
        var key = GetWaitKey(accountTokenId);

        await _database.ScriptEvaluateAsync(DecrementWaitScript, new
        {
            key = (RedisKey)key
        });
    }

    public async Task<int> GetWaitingCountAsync(Guid accountTokenId, CancellationToken cancellationToken = default)
    {
        var key = GetWaitKey(accountTokenId);
        var value = await _database.StringGetAsync(key);
        return value.HasValue ? (int)value : 0;
    }

    public async Task<bool> WaitForSlotAsync(
        Guid accountTokenId,
        Guid requestId,
        int maxConcurrency,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        const int InitialBackoffMs = 100;
        const int MaxBackoffMs = 2000;
        const double BackoffMultiplier = 1.5;
        const double JitterPercent = 0.2;

        var startTime = DateTime.UtcNow;
        var backoffMs = InitialBackoffMs;
        var random = new Random();

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (await AcquireSlotAsync(accountTokenId, requestId, maxConcurrency, cancellationToken))
            {
                return true;
            }

            // 计算抖动：±20%
            var jitter = backoffMs * JitterPercent * (random.NextDouble() * 2 - 1);
            var actualDelay = (int)(backoffMs + jitter);

            await Task.Delay(actualDelay, cancellationToken);

            // 指数退避
            backoffMs = (int)Math.Min(backoffMs * BackoffMultiplier, MaxBackoffMs);
        }

        return false;
    }

    private static string GetAccountKey(Guid accountTokenId) => $"{AccountSlotKeyPrefix}{accountTokenId}";
    private static string GetWaitKey(Guid accountTokenId) => $"{AccountWaitKeyPrefix}{accountTokenId}";
}

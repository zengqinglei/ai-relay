using AiRelay.Domain.Shared.OAuth.Authorize;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AiRelay.Infrastructure.Shared.OAuth.Authorize;

/// <summary>
/// OAuth 会话管理器实现 (基于 Redis/Memory)
/// </summary>
public class OAuthSessionManager(IDistributedCache cache) : IOAuthSessionManager
{
    private const string CacheKeyPrefix = "oauth_session:";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    public async Task<string> CreateSessionAsync(OAuthSession session, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var key = CacheKeyPrefix + sessionId;
        var value = JsonSerializer.SerializeToUtf8Bytes(session);

        await cache.SetAsync(key, value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultExpiration
        }, cancellationToken);

        return sessionId;
    }

    public async Task<OAuthSession?> GetAndRemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var key = CacheKeyPrefix + sessionId;
        var value = await cache.GetAsync(key, cancellationToken);

        if (value == null)
            return null;

        // 获取后立即移除，确保一次性使用
        await cache.RemoveAsync(key, cancellationToken);

        return JsonSerializer.Deserialize<OAuthSession>(value);
    }
}

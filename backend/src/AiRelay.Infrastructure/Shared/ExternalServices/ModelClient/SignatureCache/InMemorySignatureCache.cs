using System.Collections.Concurrent;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.SignatureCache;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.SignatureCache;

/// <summary>
/// 基于内存的签名缓存实现
/// </summary>
/// <remarks>
/// <para>线程安全：使用 ConcurrentDictionary 确保并发访问安全</para>
/// <para>过期策略：签名有效期为 30 分钟，由后台服务定期清理</para>
/// <para>分布式部署：如需多实例部署，需改用 Redis 实现</para>
/// </remarks>
public sealed class InMemorySignatureCache(
    ILogger<InMemorySignatureCache> logger) : ISignatureCache
{
    private readonly ConcurrentDictionary<string, CachedSignature> _cache = new();

    /// <summary>
    /// 签名有效期（30 分钟）
    /// </summary>
    private static readonly TimeSpan SignatureExpiration = TimeSpan.FromMinutes(30);

    private sealed record CachedSignature(string Signature, DateTime ExpiresAt);

    public void CacheSignature(string sessionId, string signature)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(signature);

        var expiresAt = DateTime.UtcNow.Add(SignatureExpiration);
        _cache[sessionId] = new CachedSignature(signature, expiresAt);

        logger.LogDebug(
            "缓存签名 - SessionId: {SessionId}, 长度: {Length}, 过期时间: {ExpiresAt:yyyy-MM-dd HH:mm:ss}",
            sessionId, signature.Length, expiresAt);
    }

    public string? GetSignature(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (!_cache.TryGetValue(sessionId, out var cached))
        {
            return null;
        }

        // 检查是否过期
        if (DateTime.UtcNow > cached.ExpiresAt)
        {
            _cache.TryRemove(sessionId, out _);
            logger.LogDebug("签名已过期 - SessionId: {SessionId}", sessionId);
            return null;
        }

        logger.LogTrace("命中签名缓存 - SessionId: {SessionId}", sessionId);
        return cached.Signature;
    }

    public void CleanupExpiredSignatures()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => now > kvp.Value.ExpiresAt)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            logger.LogInformation("清理过期签名 {Count} 个", expiredKeys.Count);
        }
    }
}

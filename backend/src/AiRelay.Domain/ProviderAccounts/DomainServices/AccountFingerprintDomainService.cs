using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;
using Leistd.Ddd.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.ProviderAccounts.DomainServices;

/// <summary>
/// 账号指纹领域服务
/// </summary>
public class AccountFingerprintDomainService(
    IRepository<AccountFingerprint, Guid> fingerprintRepository,
    IMemoryCache memoryCache,
    ILogger<AccountFingerprintDomainService> logger)
{
    private const string MaskedSessionIdCacheKeyPrefix = "AccountFingerprint:MaskedSessionId:";
    private static readonly TimeSpan MaskedSessionIdTtl = TimeSpan.FromMinutes(15);

    /// <summary>
    /// 获取或创建账号指纹
    /// </summary>
    public async Task<AccountFingerprint> GetOrCreateFingerprintAsync(
        Guid accountTokenId,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        // 尝试从数据库获取缓存的指纹
        var cached = await fingerprintRepository.GetFirstAsync(
            x => x.AccountTokenId == accountTokenId,
            cancellationToken);

        if (cached != null)
        {
            // 检查客户端的 user-agent 是否是更新版本
            headers.TryGetValue("User-Agent", out var clientUA);
            if (!string.IsNullOrEmpty(clientUA) && IsNewerVersion(clientUA, cached.UserAgent))
            {
                // 使用 Update 方法更新指纹信息（user-agent 在构造函数中设置，这里只需要保留原有其他字段）
                await fingerprintRepository.UpdateAsync(cached, cancellationToken);
                logger.LogInformation("已更新账户指纹 user-agent: {AccountTokenId}, UserAgent: {UserAgent}",
                    accountTokenId, clientUA);
            }

            return cached;
        }

        // 缓存不存在，创建新指纹
        var fingerprint = CreateFingerprintFromHeaders(accountTokenId, headers);

        // 保存到数据库
        await fingerprintRepository.InsertAsync(fingerprint, cancellationToken);

        logger.LogInformation("为账户创建新指纹，ClientId: {ClientId}, AccountTokenId: {AccountTokenId}",
            fingerprint.ClientId, accountTokenId);

        return fingerprint;
    }

    private AccountFingerprint CreateFingerprintFromHeaders(Guid accountTokenId, Dictionary<string, string> headers)
    {
        headers.TryGetValue("User-Agent", out var ua);
        var userAgent = !string.IsNullOrEmpty(ua) ? ua : ClaudeMimicDefaults.GetDefaultValue("User-Agent");

        var clientId = GenerateClientId();

        var stainlessLang = GetHeaderOrDefault(headers, "X-Stainless-Lang");
        var stainlessPackageVersion = GetHeaderOrDefault(headers, "X-Stainless-Package-Version");
        var stainlessOS = GetHeaderOrDefault(headers, "X-Stainless-Os");
        var stainlessArch = GetHeaderOrDefault(headers, "X-Stainless-Arch");
        var stainlessRuntime = GetHeaderOrDefault(headers, "X-Stainless-Runtime");
        var stainlessRuntimeVersion = GetHeaderOrDefault(headers, "X-Stainless-Runtime-Version");

        return new AccountFingerprint(
            accountTokenId,
            clientId,
            userAgent,
            stainlessLang,
            stainlessPackageVersion,
            stainlessOS,
            stainlessArch,
            stainlessRuntime,
            stainlessRuntimeVersion);
    }

    private static string GetHeaderOrDefault(Dictionary<string, string> headers, string key)
    {
        return headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : ClaudeMimicDefaults.GetDefaultValue(key);
    }

    /// <summary>
    /// 生成 64 位十六进制客户端 ID（32 字节随机数）
    /// </summary>
    public static string GenerateClientId()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// 生成会话 UUID（支持 Session ID Masking）
    /// </summary>
    /// <param name="accountTokenId">账号令牌 ID</param>
    /// <param name="sessionId">会话哈希</param>
    /// <param name="enableMasking">是否启用会话ID伪装（15分钟内固定）</param>
    public async Task<string> GenerateSessionUuidAsync(
        Guid accountTokenId,
        string? sessionId,
        bool enableMasking,
        CancellationToken cancellationToken = default)
    {
        // 如果启用 Session ID Masking，尝试获取缓存的 SessionID
        if (enableMasking)
        {
            string cacheKey = $"{MaskedSessionIdCacheKeyPrefix}{accountTokenId}";
            if (memoryCache.TryGetValue(cacheKey, out string? maskedSessionId) && !string.IsNullOrEmpty(maskedSessionId))
            {
                // 刷新 TTL（每次调用都延长 15 分钟）
                memoryCache.Set(cacheKey, maskedSessionId, MaskedSessionIdTtl);
                logger.LogDebug("使用缓存的 session ID: {SessionId}, AccountTokenId: {AccountTokenId}",
                    maskedSessionId, accountTokenId);
                return maskedSessionId;
            }
        }

        // 生成新的 SessionID
        string newSessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            newSessionId = Guid.NewGuid().ToString();
        }
        else
        {
            // 确定性生成（基于 accountId + sessionHash）
            string seed = $"{accountTokenId}::{sessionId}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            byte[] bytes = hash[..16];

            // 设置 UUID v4 版本和变体位
            bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);

            newSessionId = $"{Convert.ToHexStringLower(bytes[..4])}-" +
                           $"{Convert.ToHexStringLower(bytes[4..6])}-" +
                           $"{Convert.ToHexStringLower(bytes[6..8])}-" +
                           $"{Convert.ToHexStringLower(bytes[8..10])}-" +
                           $"{Convert.ToHexStringLower(bytes[10..16])}";
        }

        // 如果启用 Masking，缓存新生成的 SessionID
        if (enableMasking)
        {
            string cacheKey = $"{MaskedSessionIdCacheKeyPrefix}{accountTokenId}";
            memoryCache.Set(cacheKey, newSessionId, MaskedSessionIdTtl);
            logger.LogInformation("为账户创建新的 session ID: {SessionId}, AccountTokenId: {AccountTokenId}",
                newSessionId, accountTokenId);
        }

        return newSessionId;
    }

    private static bool IsNewerVersion(string newUA, string oldUA)
    {
        if (string.IsNullOrEmpty(newUA) || string.IsNullOrEmpty(oldUA))
        {
            return false;
        }

        // 提取版本号（支持格式：package/v1.2.3 或 package/1.2.3）
        var newVersion = ExtractVersion(newUA);
        var oldVersion = ExtractVersion(oldUA);

        if (newVersion == null || oldVersion == null)
        {
            return false;
        }

        return newVersion > oldVersion;
    }

    /// <summary>
    /// 从 User-Agent 提取版本号
    /// 支持格式：anthropic-sdk-typescript/0.32.1, claude-cli/v1.2.3
    /// </summary>
    private static Version? ExtractVersion(string userAgent)
    {
        // 匹配 /v1.2.3 或 /1.2.3 格式
        var match = Regex.Match(userAgent, @"/v?(\d+\.\d+(?:\.\d+)?)");
        if (match.Success && Version.TryParse(match.Groups[1].Value, out var version))
        {
            return version;
        }

        return null;
    }
}


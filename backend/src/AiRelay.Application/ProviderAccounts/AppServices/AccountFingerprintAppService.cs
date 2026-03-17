using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;

namespace AiRelay.Application.ProviderAccounts.AppServices;

/// <summary>
/// 账号指纹应用服务
/// 用于桥接 Infrastructure 层和 Domain 层
/// </summary>
public class AccountFingerprintAppService(AccountFingerprintDomainService fingerprintDomainService)
{
    /// <summary>
    /// 获取或创建账号指纹
    /// </summary>
    public Task<AccountFingerprint> GetOrCreateFingerprintAsync(
        Guid accountTokenId,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        return fingerprintDomainService.GetOrCreateFingerprintAsync(accountTokenId, headers, cancellationToken);
    }

    /// <summary>
    /// 生成会话 UUID（支持 Session ID Masking）
    /// </summary>
    public Task<string> GenerateSessionUuidAsync(
        Guid accountTokenId,
        string? sessionHash,
        bool enableMasking,
        CancellationToken cancellationToken = default)
    {
        return fingerprintDomainService.GenerateSessionUuidAsync(accountTokenId, sessionHash, enableMasking, cancellationToken);
    }
}

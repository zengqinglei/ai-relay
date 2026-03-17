using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ProviderAccounts.Entities;

/// <summary>
/// OAuth 账号指纹（用于生成 metadata.user_id）
/// </summary>
public class AccountFingerprint : FullAuditedEntity<Guid>
{
    /// <summary>
    /// 关联的账号令牌 ID
    /// </summary>
    public Guid AccountTokenId { get; private set; }

    /// <summary>
    /// 客户端 ID（64位十六进制，32字节随机数）
    /// </summary>
    public string ClientId { get; private set; } = string.Empty;

    /// <summary>
    /// User-Agent（从首次请求提取，支持版本更新）
    /// </summary>
    public string UserAgent { get; private set; } = string.Empty;

    public string? StainlessLang { get; private set; }
    public string? StainlessPackageVersion { get; private set; }
    public string? StainlessOS { get; private set; }
    public string? StainlessArch { get; private set; }
    public string? StainlessRuntime { get; private set; }
    public string? StainlessRuntimeVersion { get; private set; }

    private AccountFingerprint() { }

    public AccountFingerprint(
        Guid accountTokenId,
        string clientId,
        string userAgent,
        string? stainlessLang = null,
        string? stainlessPackageVersion = null,
        string? stainlessOS = null,
        string? stainlessArch = null,
        string? stainlessRuntime = null,
        string? stainlessRuntimeVersion = null)
    {
        Id = Guid.CreateVersion7();
        AccountTokenId = accountTokenId;
        ClientId = clientId;
        UserAgent = userAgent;
        StainlessLang = stainlessLang;
        StainlessPackageVersion = stainlessPackageVersion;
        StainlessOS = stainlessOS;
        StainlessArch = stainlessArch;
        StainlessRuntime = stainlessRuntime;
        StainlessRuntimeVersion = stainlessRuntimeVersion;
    }

    public void Update(
        string? stainlessLang = null,
        string? stainlessPackageVersion = null,
        string? stainlessOS = null,
        string? stainlessArch = null,
        string? stainlessRuntime = null,
        string? stainlessRuntimeVersion = null)
    {
        if (stainlessLang != null) StainlessLang = stainlessLang;
        if (stainlessPackageVersion != null) StainlessPackageVersion = stainlessPackageVersion;
        if (stainlessOS != null) StainlessOS = stainlessOS;
        if (stainlessArch != null) StainlessArch = stainlessArch;
        if (stainlessRuntime != null) StainlessRuntime = stainlessRuntime;
        if (stainlessRuntimeVersion != null) StainlessRuntimeVersion = stainlessRuntimeVersion;
    }
}

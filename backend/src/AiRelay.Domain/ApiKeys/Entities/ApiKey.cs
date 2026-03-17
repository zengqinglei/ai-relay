using AiRelay.Domain.ApiKeys.Events;
using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.ApiKeys.Entities;

/// <summary>
/// API Key 实体
/// </summary>
public class ApiKey : DeletionAuditedEntity<Guid>
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// API 密钥加密值（AES 加密存储）
    /// </summary>
    public string EncryptedSecret { get; private set; }

    /// <summary>
    /// API 密钥哈希值（HMAC-SHA256，确定性，用于唯一性查找）
    /// </summary>
    public string SecretHash { get; private set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// 过期时间（null 表示永不过期）
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    public virtual ICollection<ApiKeyProviderGroupBinding> Bindings { get; private set; } = new List<ApiKeyProviderGroupBinding>();

    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    }

    public bool IsValid()
    {
        return IsActive && !IsExpired() && !IsDeleted;
    }

    private ApiKey()
    {
        Name = null!;
        EncryptedSecret = null!;
        SecretHash = null!;
    }

    public ApiKey(
        string name,
        string? description,
        string encryptedSecret,
        string secretHash,
        DateTime? expiresAt = null)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Description = description;
        EncryptedSecret = encryptedSecret;
        SecretHash = secretHash;
        ExpiresAt = expiresAt;

        AddLocalEvent(new ApiKeyCreatedEvent(Id, Name));
    }

    public void Enable() => IsActive = true;

    public void Disable() => IsActive = false;

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void UpdateExpiration(DateTime? expiresAt)
    {
        ExpiresAt = expiresAt;
    }

    public void RecordUsage()
    {
        LastUsedAt = DateTime.UtcNow;
    }

    public void MarkAsDeleted()
    {
        AddLocalEvent(new ApiKeyDeletedEvent(Id, Name, EncryptedSecret));
    }
}
